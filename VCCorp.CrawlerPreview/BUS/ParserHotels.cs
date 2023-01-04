using CefSharp.WinForms;
using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using VCCorp.CrawlerCore.DTO;
using VCCorp.CrawlerPreview.DAO;
using VCCorp.CrawlerPreview.DTO;

namespace VCCorp.CrawlerPreview.BUS
{
    public class ParserHotels
    {
        private ChromiumWebBrowser _browser = null;
        private readonly HtmlAgilityPack.HtmlDocument _document = new HtmlAgilityPack.HtmlDocument();
        private const string _jsClickShowMoreReview = @"document.getElementsByClassName('uitk-button uitk-button-medium uitk-button-has-text uitk-button-secondary')[0].click()";
        private const string _jsClickShowMoreHotel = @"document.getElementsByClassName('uitk-button uitk-button-medium uitk-button-secondary')[0].click()";
        private const string _jsAutoScroll = @"window.scrollTo(0, document.body.scrollHeight/2)";
        private const string _jsAutoScroll1 = @"function pageScroll() {window.scrollBy(0,50);scrolldelay = setTimeout(pageScroll,50);}{window.scrollBy(0,50);scrolldelay = setTimeout(pageScroll,50);}";
        private string URL_HOTELS = "https://vi.hotels.com/";
        public ParserHotels()
        {
        }
        public ParserHotels(ChromiumWebBrowser browser)
        {
            _browser = browser;
        }

        public async Task CrawlData()
        {
            await GetListHotel();
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
            ushort indexLastContent = 0;
            try
            {
                string url = "https://vi.hotels.com/Hotel-Search?destination=H%C3%A0%20N%E1%BB%99i%2C%20Vi%C3%AA%CC%A3t%20Nam";
                await _browser.LoadUrlAsync(url);
                await Task.Delay(5_000);

                await Common.Utilities.EvaluateJavaScriptSync(_jsAutoScroll, _browser).ConfigureAwait(false);
                await Common.Utilities.EvaluateJavaScriptSync(_jsAutoScroll1, _browser).ConfigureAwait(false);
                await Task.Delay(15_000);

                byte i = 0;
                while (i < 20)
                {
                    i++;
                    string checkJs1 = await Common.Utilities.EvaluateJavaScriptSync(_jsClickShowMoreHotel, _browser).ConfigureAwait(false);
                    if (checkJs1 == null)
                    {
                        break;
                    }
                    await Task.Delay(5_000);

                }
                
                while (true)
                {
                    string html = await Common.Utilities.GetBrowserSource(_browser).ConfigureAwait(false);
                    _document.LoadHtml(html);
                    html = null;

                    HtmlNodeCollection divComment = _document.DocumentNode.SelectNodes($"//div[contains(@class,'uitk-card uitk-card-roundcorner-all uitk-card-has-primary-theme')][position()>{indexLastContent}]");
                    if (divComment == null)
                    {
                        break;
                    }

                    if (divComment != null)
                    {
                        foreach (HtmlNode item in divComment)
                        {
                            string listURL = item.SelectSingleNode(".//a[contains(@class,'uitk-card-link')]")?.Attributes["href"]?.Value ?? "";
                            //loại bỏ kí tự đằng sau dấu '?' chỉ lấy id hotel
                            listURL = Regex.Replace(listURL, @"\?[\s\S]+", " ", RegexOptions.IgnoreCase);
                            ContentDTO content = new ContentDTO();
                            content.ReferUrl = listURL;
                            content.CreateDate = DateTime.Now; // ngày bóc tách
                            content.Domain = URL_HOTELS;
                            content.Status = 0;
                            contentList.Add(content);

                            ContentDAO msql = new ContentDAO(ConnectionDAO.ConnectionToTableLinkProduct);
                            await msql.InsertLinkContent(content);
                            msql.Dispose();

                            indexLastContent++;
                        }
                    }
                   

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
            ushort indexLastComment = 0;
            try
            {
                ContentDAO contentDAO = new ContentDAO(ConnectionDAO.ConnectionToTableLinkProduct);
                List<ContentDTO> dataUrl = contentDAO.GetLinkByDomain(URL_HOTELS);
                contentDAO.Dispose();
                for (int i = 0; i < dataUrl.Count; i++)
                {
                    int status = dataUrl[i].Status;
                    if (status == 0)// check xem đã bóc hay chưa?
                    {
                        string url = URL_HOTELS + dataUrl[i].ReferUrl;
                        await _browser.LoadUrlAsync(url);
                        await Task.Delay(10_000);

                        string html = await Common.Utilities.GetBrowserSource(_browser).ConfigureAwait(false);
                        _document.LoadHtml(html);
                        html = null;


                        //Lấy hotel details
                        ContentDTO content = new ContentDTO();
                        content.Subject = _document.DocumentNode.SelectSingleNode("//h1[contains(@class,'uitk-heading uitk-heading-3')]")?.InnerText;
                        content.Contents = Common.Utilities.RemoveSpecialCharacter(_document.DocumentNode.SelectSingleNode("(//div[contains(@data-stid,'content-markup')]//div[contains(@class,'uitk-text-default-theme')])[1]")?.InnerText);
                        content.ImageThumb = _document.DocumentNode.SelectSingleNode("//div[contains(@class,'uitk-image-placeholder')]//img")?.Attributes["src"]?.Value ?? "";
                        content.Domain = URL_HOTELS;
                        content.ReferUrl = url;
                        content.CreateDate = DateTime.Now;

                        //Lưu vào Db
                        ContentDAO msql = new ContentDAO(ConnectionDAO.ConnectionToTableLinkProduct);
                        await msql.InserContent(content);
                        msql.Dispose();

                        //Update Status (crawled == 1 )
                        ContentDAO msql1 = new ContentDAO(ConnectionDAO.ConnectionToTableLinkProduct);
                        await msql1.UpdateStatus(dataUrl[i].ReferUrl);
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
                        //await kafka.InsertPost(jsonPost, "crawler-preview-post");
                        #endregion

                        while (true)
                        {
                            string html1 = await Common.Utilities.GetBrowserSource(_browser).ConfigureAwait(false);
                            _document.LoadHtml(html1);
                            html1 = null;
                            //Lấy list comment
                            HtmlNodeCollection divComment = _document.DocumentNode.SelectNodes($"//div[contains(@class,'uitk-card-content-section')]//article[position()>{indexLastComment}]");
                            HtmlNode contentComment = _document.DocumentNode.SelectSingleNode("//section[contains(@class,'uitk-spacing uitk-spacing-margin-block-two')]//div[contains(@class,'uitk-type-300 uitk-text-default-theme')]/span");
                            if (divComment == null)
                            {
                                break;
                            }

                            if (divComment != null)
                            {
                                foreach (HtmlNode item in divComment)
                                {
                                    if (contentComment != null)
                                    {
                                        DTO.CommentDTO commentDTO = new DTO.CommentDTO();
                                        commentDTO.Point = item.SelectSingleNode(".//div[contains(@class,'ratePoint__numb')]/span")?.InnerText;
                                        string author = Regex.Match(item.SelectSingleNode(".//div[contains(@class,'itk-layout-flex-gap-two')]//div[contains(@class,'uitk-type-200 uitk-text-default-theme uitk-layout-flex-item')]")?.InnerText, @"^\w+").Value;
                                        commentDTO.Author = author;
                                        commentDTO.ContentsComment = Common.Utilities.RemoveSpecialCharacter(item.SelectSingleNode(".//section[contains(@class,'uitk-spacing uitk-spacing-margin-block-two')]//div[contains(@class,'uitk-type-300 uitk-text-default-theme')]/span")?.InnerText);

                                        DateTime postDate = DateTime.Now;
                                        string datecomment = item.SelectSingleNode(".//section/div[contains(@class,'uitk-type-300 uitk-text-default-theme')]/span").InnerText;
                                        if (!string.IsNullOrEmpty(datecomment))
                                        {
                                            Common.DateTimeFormatAgain dtFomat = new Common.DateTimeFormatAgain();
                                            string date = dtFomat.GetDateByPattern(datecomment, "dd/MM/yyyy");

                                            try
                                            {
                                                postDate = Convert.ToDateTime(date);
                                            }
                                            catch { }
                                        }
                                        commentDTO.PostDate = postDate;
                                        commentDTO.CreateDate = DateTime.Now;
                                        commentDTO.Domain = URL_HOTELS;
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
                                        //await kafka1.InsertPost(jsonPost1, "crawler-preview-post-comment");
                                        #endregion

                                        indexLastComment++;
                                    }

                                }
                            }
                            //Check nút xem thêm
                            HtmlNode checkMoreItem = _document.DocumentNode.SelectSingleNode("//button[contains(@class,'uitk-button uitk-button-medium uitk-button-has-text uitk-button-secondary')]");
                            if (checkMoreItem == null)
                            {
                                break;
                            }
                            string checkJs = await Common.Utilities.EvaluateJavaScriptSync(_jsClickShowMoreReview, _browser).ConfigureAwait(false);
                            if (checkJs == null)
                            {
                                break;
                            }
                            await Task.Delay(10_000);
                        }
                    }
                }
            }
            catch { }
            return commentList;
        }
    }
}
