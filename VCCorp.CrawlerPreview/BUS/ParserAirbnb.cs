using CefSharp.WinForms;
using HtmlAgilityPack;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.UI.WebControls;
using System.Xml.Linq;
using VCCorp.CrawlerCore.BUS;
using VCCorp.CrawlerCore.Common;
using VCCorp.CrawlerCore.DTO;
using VCCorp.CrawlerPreview.Common;
using VCCorp.CrawlerPreview.DAO;
using VCCorp.CrawlerPreview.DTO;

namespace VCCorp.CrawlerPreview.BUS
{
    public class ParserAirbnb
    {
        private ChromiumWebBrowser _browser = null;
        private readonly HtmlAgilityPack.HtmlDocument _document = new HtmlAgilityPack.HtmlDocument();
        private string URL_AIRBNB = "https://www.airbnb.com.vn";
        private ConcurrentQueue<ContentDTO> _myQueue = new ConcurrentQueue<ContentDTO>();
        private const string _jsAutoScroll = @"function pageScroll() {window.scrollBy(0,10);scrolldelay = setTimeout(pageScroll,50);}{window.scrollBy(0,10);scrolldelay = setTimeout(pageScroll,50);}";
        private const string _jsClickShowMoreReview = @"document.getElementsByClassName('b65jmrv')[1].click()";

        public ParserAirbnb()
        {
        }
        public ParserAirbnb(ChromiumWebBrowser browser)
        {
            _browser = browser;
        }

        public async Task CrawlData()
        {
            await GetListHotel(URL_AIRBNB);
            await GetHotelDetail();

        }
        /// <summary>
        /// Lấy list hotel
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        public async Task<List<ContentDTO>> GetListHotel(string url)
        {
            List<ContentDTO> contentList = new List<ContentDTO>();
            ushort indexLastContent = 0;
            try
            {
                await _browser.LoadUrlAsync(url);
                await Task.Delay(10_000);

                while (true)
                {
                    string html = await Common.Utilities.GetBrowserSource(_browser).ConfigureAwait(false);
                    _document.LoadHtml(html);
                    html = null;

                    HtmlNodeCollection divComment = _document.DocumentNode.SelectNodes($"//div[contains(@class,'gh7uyir')]/div[contains(@class,' dir dir-ltr')][position()>{indexLastContent}]");
                    if (divComment == null)
                    {
                        break;
                    }

                    if (divComment != null)
                    {
                        foreach (HtmlNode item in divComment)
                        {
                            string listURL = item.SelectSingleNode(".//div[contains(@class,'cy5jw6o')]/a")?.Attributes["href"]?.Value ?? "";
                            //loại bỏ kí tự đằng sau dấu '?' chỉ lấy id hotel
                            listURL = Regex.Replace(listURL, @"\?[\s\S]+", " ", RegexOptions.IgnoreCase);
                            if (string.IsNullOrEmpty(listURL))
                            {
                                break;
                            }
                            ContentDTO content = new ContentDTO();
                            content.ReferUrl = listURL;

                            content.Domain = URL_AIRBNB;
                            content.CreateDate = DateTime.Now; // ngày bóc tách
                            content.CreateDate_Timestamp = Common.Utilities.DateTimeToUnixTimestamp(DateTime.Now); // ngày bóc tách chuyển sang dạng Timestamp
                            contentList.Add(content);

                            ContentDAO msql1 = new ContentDAO(ConnectionDAO.ConnectionToTableLinkProduct);
                            await msql1.InsertLinkContent(content);
                            msql1.Dispose();

                            indexLastContent++;

                        }
                    }
                    string checkJs = await Common.Utilities.EvaluateJavaScriptSync(_jsAutoScroll, _browser).ConfigureAwait(false);
                    if (checkJs == null)
                    {
                        break;
                    }

                    await Task.Delay(25_000);
                }
            }
            catch { }
            return contentList;
        }


        /// <summary>
        /// Lấy nội dung bài viết + bình luận
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        public async Task<List<DTO.CommentDTO>> GetHotelDetail()
        {
            List<DTO.CommentDTO> commentList = new List<DTO.CommentDTO>();
            try
            {
                //Lấy list Url hotel từ Db
                ContentDAO contentDAO = new ContentDAO(ConnectionDAO.ConnectionToTableLinkProduct);
                List<ContentDTO> dataUrl = contentDAO.GetLinkByDomain(URL_AIRBNB);
                contentDAO.Dispose();

                //Đọc từng Url
                for (int i = 0; i < dataUrl.Count; i++)
                {
                    string url = URL_AIRBNB + dataUrl[i].ReferUrl;
                    await _browser.LoadUrlAsync(url);
                    await Task.Delay(10_000);


                    string html = await Common.Utilities.GetBrowserSource(_browser).ConfigureAwait(false);
                    _document.LoadHtml(html);
                    html = null;

                    // Lấy detail content hotel
                    ContentDTO content = new ContentDTO();
                    content.TotalPoint = _document.DocumentNode.SelectSingleNode("//span[contains(@class,'_17p6nbba')]")?.InnerText;
                    content.Subject = _document.DocumentNode.SelectSingleNode("//span[contains(@class,'_1n81at5')]/h1")?.InnerText;
                    content.Contents = Common.Utilities.RemoveSpecialCharacter(_document.DocumentNode.SelectSingleNode("//div[contains(@class,'d1isfkwk')]//span[contains(@class,'ll4r2nl')]")?.InnerText);
                    content.ImageThumb = _document.DocumentNode.SelectSingleNode("//div[contains(@class,'cgx2eil')]//img")?.Attributes["src"]?.Value ?? "";
                    content.Summary = _document.DocumentNode.SelectSingleNode("//div[contains(@class,'_cv5qq4')]//h2")?.InnerText;
                    content.Domain = URL_AIRBNB;
                    content.ReferUrl = url;
                    content.CreateDate = DateTime.Now;

                    //Lưu vào Db
                    ContentDAO msql = new ContentDAO(ConnectionDAO.ConnectionToTableLinkProduct);
                    await msql.InserContent(content);
                    msql.Dispose();

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

                    //Lấy list comment
                    HtmlNodeCollection divComment = _document.DocumentNode.SelectNodes("//div[contains(@class,'r1rl3yjt')]");
                    if (divComment != null)
                    {
                        foreach (HtmlNode item in divComment)
                        {
                            DTO.CommentDTO commentDTO = new DTO.CommentDTO();
                            commentDTO.Author = item.SelectSingleNode(".//div[contains(@class,'t9gtck5')]/h3")?.InnerText;
                            commentDTO.ContentsComment = Common.Utilities.RemoveSpecialCharacter(item.SelectSingleNode(".//span[contains(@class,'ll4r2nl')]")?.InnerText);
                            DateTime postDate = DateTime.Now;
                            string datecomment = item.SelectSingleNode(".//li[contains(@class,'_1f1oir5')]").InnerText;
                            if (!string.IsNullOrEmpty(datecomment))
                            {
                                Common.DateTimeFormatAgain dtFomat = new Common.DateTimeFormatAgain();
                                string date = dtFomat.GetDateByPattern(datecomment, "MM/yyyy");
                                try
                                {
                                    postDate = Convert.ToDateTime(date);
                                }
                                catch { }
                            }
                            commentDTO.PostDate = postDate;
                            commentDTO.CreateDate = DateTime.Now;
                            commentDTO.Domain = URL_AIRBNB;
                            commentDTO.ReferUrl = url;
                            commentList.Add(commentDTO);

                            //Lưu vào Db
                            CommentDAO msql1 = new CommentDAO(ConnectionDAO.ConnectionToTableLinkProduct);
                            await msql1.InsertListComment(commentDTO);
                            msql1.Dispose();

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
                            enti.Get_Time_String = commentDTO.PostDate.ToString("yyyy-MM-dd HH:mm:ss");

                            string jsonPost1 = KafkaPreview.ToJson<ArticleDTO_BigData>(enti);
                            KafkaPreview kafka1 = new KafkaPreview();
                            //await kafka1.InsertPost(jsonPost1, "crawler-preview-post-comment");
                            #endregion
                        }
                    }

                }
            }
            catch { }
            return commentList;
        }

    }
}
