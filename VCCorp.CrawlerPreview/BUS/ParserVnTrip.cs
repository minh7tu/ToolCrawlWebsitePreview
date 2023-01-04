using CefSharp.WinForms;
using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using VCCorp.CrawlerCore.BUS;
using VCCorp.CrawlerCore.Common;
using VCCorp.CrawlerCore.DTO;
using VCCorp.CrawlerPreview.DAO;
using VCCorp.CrawlerPreview.DTO;

namespace VCCorp.CrawlerPreview.BUS
{
    public class ParserVnTrip
    {
        private ChromiumWebBrowser _browser = null;
        private readonly HtmlAgilityPack.HtmlDocument _document = new HtmlAgilityPack.HtmlDocument();
        private const string _jsClickShowMoreReview = @"document.getElementsByClassName('ant-pagination-item-link')[1].click()";
        private const string _jsClickShowMoreReview2 = @"document.getElementsByClassName('ant-pagination-item-link')[2].click()";
        private const string _jsAutoScroll = @"function pageScroll() {window.scrollBy(0,10);scrolldelay = setTimeout(pageScroll,30);}{window.scrollBy(0,10);scrolldelay = setTimeout(pageScroll,30);}";
        private string URL_VNTRIP = "https://www.vntrip.vn";

        public ParserVnTrip()
        {
        }
        public ParserVnTrip(ChromiumWebBrowser browser)
        {
            _browser = browser;
        }

        public async Task CrawlData()
        {
            //await GetListHotel();
            await GetHotelDetail();

        }
        /// <summary>
        /// Lấy list hotel
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>

        public async Task<List<ContentDTO>> GetListHotel()
        {
            List<ContentDTO> contentList = new List<ContentDTO>();

            try
            {
                for (int i = 1; i < 9999; i++)
                {
                    string url = "https://www.vntrip.vn/tim-khach-san?keyword=da-nang&page=" + i;
                    await _browser.LoadUrlAsync(url);
                    await Task.Delay(10_000);

                    string html = await Common.Utilities.GetBrowserSource(_browser).ConfigureAwait(false);
                    _document.LoadHtml(html);
                    html = null;

                    HtmlNodeCollection divComment = _document.DocumentNode.SelectNodes("//div[contains(@class,'listHotel__item')]");
                    if (divComment == null)
                    {
                        break;
                    }

                    if (divComment != null)
                    {
                        foreach (HtmlNode item in divComment)
                        {
                            string listURL = item.SelectSingleNode(".//a[contains(@class,'hotelItem__link')]")?.Attributes["href"]?.Value ?? "";
                            //loại bỏ kí tự đằng sau dấu '?' chỉ lấy id hotel
                            listURL = Regex.Replace(listURL, @"\?[\s\S]+", " ", RegexOptions.IgnoreCase);
                            ContentDTO content = new ContentDTO();
                            content.ReferUrl = listURL;
                            content.CreateDate = DateTime.Now; // ngày bóc tách
                            content.CreateDate_Timestamp = Common.Utilities.DateTimeToUnixTimestamp(DateTime.Now); // ngày bóc tách chuyển sang dạng Timestamp
                            content.Domain = URL_VNTRIP;
                            content.Status = 0;
                            contentList.Add(content);

                            ContentDAO msql = new ContentDAO(ConnectionDAO.ConnectionToTableLinkProduct);
                            await msql.InsertLinkContent(content);
                            msql.Dispose();

                        }
                    }

                    await Task.Delay(10_000);
                }

            }
            catch { }
            return contentList;
        }

        /// <summary>
        /// Lấy nội dung bình luận
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        public async Task<List<DTO.CommentDTO>> GetHotelDetail()
        {
            List<DTO.CommentDTO> commentList = new List<DTO.CommentDTO>();
            try
            {
                ContentDAO contentDAO = new ContentDAO(ConnectionDAO.ConnectionToTableLinkProduct);
                List<ContentDTO> dataUrl = contentDAO.GetLinkByDomain(URL_VNTRIP);
                contentDAO.Dispose();
                for (int i = 0; i < dataUrl.Count; i++)
                {
                    int status = dataUrl[i].Status;
                    if (status == 0)// check xem đã bóc hay chưa?
                    {
                        string url = URL_VNTRIP + dataUrl[i].ReferUrl;
                        string referUrl = dataUrl[i].ReferUrl;
                        //string url = "https://www.vntrip.vn/hotel/vn/kieu-anh-hotel-11384?checkInDate=20221202&nights=1";
                        await _browser.LoadUrlAsync(url);
                        await Task.Delay(10_000);

                        string html = await Common.Utilities.GetBrowserSource(_browser).ConfigureAwait(false);
                        _document.LoadHtml(html);
                        html = null;

                        //Lấy hotel details
                        ContentDTO content = new ContentDTO();
                        content.TotalPoint = _document.DocumentNode.SelectSingleNode("//div[contains(@class,'hotelDetail__rate')]//span")?.InnerText;
                        content.Subject = _document.DocumentNode.SelectSingleNode("//h1[contains(@class,'hotelName')]")?.InnerText;
                        content.Contents = Common.Utilities.RemoveSpecialCharacter(_document.DocumentNode.SelectSingleNode("//div[contains(@class,'pText')]//p")?.InnerText);
                        content.ImageThumb = _document.DocumentNode.SelectSingleNode("//div[contains(@class,'hotelDetail__body')]//img")?.Attributes["src"]?.Value ?? "";
                        content.Domain = URL_VNTRIP;
                        content.ReferUrl = url;
                        content.CreateDate = DateTime.Now;

                        //Lưu vào Db
                        ContentDAO msql = new ContentDAO(ConnectionDAO.ConnectionToTableLinkProduct);
                        await msql.InserContent(content);
                        msql.Dispose();

                        //Update Status (crawled == 1 )
                        ContentDAO msql1 = new ContentDAO(ConnectionDAO.ConnectionToTableLinkProduct);
                        await msql1.UpdateStatus(referUrl);
                        msql1.Dispose();

                        #region gửi đi cho ILS

                        ArticleDTO_BigData ent = new ArticleDTO_BigData();

                        ent.Id = Common.Utilities.Md5Encode(content.Id);
                        ent.Content = content.Contents;

                        //Get_Time là thời gian bóc 
                        ent.Get_Time = content.CreateDate;
                        ent.Get_Time_String = content.CreateDate.ToString("yyyy-MM-dd HH:mm:ss");

                        ent.Description = content.Summary;

                        ent.Title = content.Subject;
                        ent.Url = content.ReferUrl;
                        ent.Source_Id = 0;
                        ent.Category = content.Category;
                        ent.Image = content.ImageThumb;
                        ent.urlAmphtml = "";

                        ent.ContentNoRemoveHtml = ""; // xóa đi khi lưu xuống cho nhẹ

                        string jsonPost = KafkaPreview.ToJson<ArticleDTO_BigData>(ent);
                        KafkaPreview kafka = new KafkaPreview();
                        await kafka.InsertPost(jsonPost, "crawler-preview-post");
                        #endregion

                        while (true)
                        {
                            string html1 = await Common.Utilities.GetBrowserSource(_browser).ConfigureAwait(false);
                            _document.LoadHtml(html1);
                            html1 = null;
                            //Lấy list comment
                            HtmlNodeCollection divComment = _document.DocumentNode.SelectNodes("//li[contains(@class,'hotelFeedback__item')]");
                            if (divComment != null)
                            {
                                foreach (HtmlNode item in divComment)
                                {
                                    DTO.CommentDTO commentDTO = new DTO.CommentDTO();
                                    commentDTO.Author = item.SelectSingleNode(".//p[contains(@class,'p1')]")?.InnerText;
                                    commentDTO.Point = item.SelectSingleNode(".//div[contains(@class,'ratePoint__numb')]/span")?.InnerText;
                                    string comment1 = Regex.Match(Common.Utilities.RemoveSpecialCharacter(item.SelectSingleNode(".//p[contains(@class,'hotelFeedback__text')][1]")?.InnerText), @"(?<=&nbsp;)[\s\S]+").Value;
                                    string comment2 = Regex.Match(Common.Utilities.RemoveSpecialCharacter(item.SelectSingleNode(".//p[contains(@class,'hotelFeedback__text')][2]")?.InnerText), @"(?<=&nbsp;)[\s\S]+").Value;

                                    commentDTO.ContentsComment = comment1 + "." + comment2;
                                    DateTime postDate = DateTime.Now;
                                    string datecomment = item.SelectSingleNode(".//p[contains(@class,'p2')]").InnerText;
                                    if (!string.IsNullOrEmpty(datecomment))
                                    {
                                        Common.DateTimeFormatAgain dtFomat = new Common.DateTimeFormatAgain();
                                        string date = dtFomat.GetDate(datecomment, "dd/MM/yyyy");
                                        try
                                        {
                                            postDate = Convert.ToDateTime(date);
                                        }
                                        catch { }
                                    }
                                    commentDTO.PostDate = postDate;
                                    commentDTO.CreateDate = DateTime.Now;
                                    commentDTO.Domain = URL_VNTRIP;
                                    commentDTO.ReferUrl = url;
                                    commentList.Add(commentDTO);

                                    //Lưu về db
                                    CommentDAO msql2 = new CommentDAO(ConnectionDAO.ConnectionToTableLinkProduct);
                                    await msql2.InsertListComment(commentDTO);
                                    msql2.Dispose();

                                    #region gửi đi cho ILS

                                    ArticleDTO_BigData enti = new ArticleDTO_BigData();

                                    enti.Comment = commentDTO.ContentsComment;
                                    enti.Author = commentDTO.Author;
                                    enti.Url = commentDTO.ReferUrl;
                                    // thời gian tạo tin
                                    enti.Create_time = commentDTO.PostDate;
                                    enti.Create_Time_String = commentDTO.PostDate.ToString("yyyy-MM-dd HH:mm:ss");

                                    //Get_Time là thời gian bóc 
                                    enti.Get_Time = commentDTO.CreateDate;
                                    enti.Get_Time_String = commentDTO.CreateDate.ToString("yyyy-MM-dd HH:mm:ss");

                                    string jsonPost1 = KafkaPreview.ToJson<ArticleDTO_BigData>(enti);
                                    KafkaPreview kafka1 = new KafkaPreview();
                                    await kafka1.InsertPost(jsonPost1, "crawler-preview-post-comment");
                                    #endregion

                                }
                            }

                            //Check nút xem thêm
                            HtmlNode checkMoreItem = _document.DocumentNode.SelectSingleNode("//span[contains(@class,'ant-pagination-item-ellipsis')]");
                            HtmlNode checkEnd = _document.DocumentNode.SelectSingleNode("//li[contains(@class,'ant-pagination-next')]/button[@disabled]");
                            if (checkMoreItem == null)
                            {
                                if (checkEnd != null)
                                {
                                    break;
                                }
                                else
                                {
                                    string checkJs = await Common.Utilities.EvaluateJavaScriptSync(_jsClickShowMoreReview, _browser).ConfigureAwait(false);
                                    if (checkJs == null)
                                    {
                                        break;
                                    }
                                    await Task.Delay(10_000);
                                }
                            }
                            else
                            {
                                if (checkEnd != null)
                                {
                                    break;
                                }
                                else
                                {
                                    string checkJs1 = await Common.Utilities.EvaluateJavaScriptSync(_jsClickShowMoreReview2, _browser).ConfigureAwait(false);
                                    if (checkJs1 == null)
                                    {
                                        break;
                                    }
                                    await Task.Delay(10_000);
                                }
                            }
                        }
                    }

                }
            }
            catch { }
            return commentList;
        }
    }
}
