using CefSharp.WinForms;
using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using VCCorp.CrawlerCore.DTO;
using VCCorp.CrawlerPreview.DAO;
using VCCorp.CrawlerPreview.DTO;

namespace VCCorp.CrawlerPreview.BUS
{
    public class ParserPasGo
    {
        private ChromiumWebBrowser _browser = null;
        private readonly HtmlAgilityPack.HtmlDocument _document = new HtmlAgilityPack.HtmlDocument();
        private const string _jsClickShowMoreReview = @"document.getElementsByClassName('MuiButtonBase-root MuiButton-root MuiButton-text jss243')[0].click()";
        private const string _jsAutoScroll = @"function pageScroll() {window.scrollBy(0,10);scrolldelay = setTimeout(pageScroll,30);}{window.scrollBy(0,10);scrolldelay = setTimeout(pageScroll,30);}";
        private string URL_PASGO = "https://pasgo.vn/";
        private string URL_PASGO_LAU = "https://pasgo.vn/tim-kiem?search=L%u1ea9u&page=";
        private string URL_PASGO_NUONG = "https://pasgo.vn/tim-kiem?search=N%u01b0%u1edbng&page=";
        private string URL_PASGO_BUFFE = "https://pasgo.vn/tim-kiem?search=Buffet&page=";
        private string URL_PASGO_HAISAN = "https://pasgo.vn/ha-noi/nha-hang/hai-san-28?page=";
        private string URL_PASGO_NHAU = "https://pasgo.vn/ha-noi/nha-hang/quan-nhau-165?page=";
        private string URL_PASGO_NHAT = "https://pasgo.vn/ha-noi/nha-hang/nhat-ban-15?page=";
        private string URL_PASGO_HAN = "https://pasgo.vn/tim-kiem?search=m%u00f3n+H%u00e0n&page=";
        private string URL_PASGO_VN = "https://pasgo.vn/tim-kiem?search=m%u00f3n+Vi%u1ec7t&page=";

        public ParserPasGo(ChromiumWebBrowser browser)
        {
            _browser = browser;
        }

        public async Task CrawlData()
        {
            //await GetListResName();
            await GetPasgoDetail();
        }
        /// <summary>
        /// Lấy list hotel
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        public async Task<List<ContentDTO>> GetListResName()
        {
            List<ContentDTO> contentList = new List<ContentDTO>();
            try
            {
                for (int i = 1; i < 999; i++)
                {
                    string url = URL_PASGO_BUFFE + i;
                    await _browser.LoadUrlAsync(url);
                    await Task.Delay(10_000);
                    string html = await Common.Utilities.GetBrowserSource(_browser).ConfigureAwait(false);
                    _document.LoadHtml(html);
                    html = null;

                    HtmlNodeCollection divComment = _document.DocumentNode.SelectNodes("//div[contains(@class,'wapitem')]");
                    if (divComment == null)
                    {
                        break;
                    }

                    if (divComment != null)
                    {
                        foreach (HtmlNode item in divComment)
                        {
                            string listURL = item.SelectSingleNode(".//a[contains(@class,'waptop')]")?.Attributes["href"]?.Value ?? "";
                            //loại bỏ kí tự đằng sau dấu '?' chỉ lấy id hotel
                            listURL = Regex.Replace(listURL, @"\?[\s\S]+", " ", RegexOptions.IgnoreCase);
                            ContentDTO content = new ContentDTO();
                            content.ReferUrl = listURL;
                            content.CreateDate = DateTime.Now; // ngày bóc tách
                            content.CreateDate_Timestamp = Common.Utilities.DateTimeToUnixTimestamp(DateTime.Now); // ngày bóc tách chuyển sang dạng Timestamp
                            content.Domain = URL_PASGO;
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
        /// Lấy nội dung bài viết và nội dung bình luận
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        public async Task<List<DTO.CommentDTO>> GetPasgoDetail()
        {
            List<DTO.CommentDTO> commentList = new List<DTO.CommentDTO>();
            
            try
            {
                //Lấy url hotel từ db
                ContentDAO contentDAO = new ContentDAO(ConnectionDAO.ConnectionToTableLinkProduct);
                List<ContentDTO> dataUrl = contentDAO.GetLinkByDomain(URL_PASGO);
                contentDAO.Dispose();

                //Đọc từng url để bóc
                for (int i = 0; i < dataUrl.Count; i++)
                {
                    int status = dataUrl[i].Status;
                    if (status == 0)// check xem đã bóc hay chưa?
                    {
                        string url = dataUrl[i].ReferUrl;

                        await _browser.LoadUrlAsync(url);
                        await Task.Delay(10_000);

                        string html = await Common.Utilities.GetBrowserSource(_browser).ConfigureAwait(false);
                        _document.LoadHtml(html);
                        html = null;

                        //Bóc hotel detail
                        ContentDTO content = new ContentDTO();
                        content.Subject = _document.DocumentNode.SelectSingleNode("//span[contains(@class,'pasgo-title')]")?.InnerText;
                        content.ImageThumb = _document.DocumentNode.SelectSingleNode("//div[contains(@class,'item')]//img")?.Attributes["src"]?.Value ?? "";
                        content.Category = _document.DocumentNode.SelectSingleNode("//div[contains(@class,'col-md-5 right')]//span").InnerText;
                        content.Contents = Common.Utilities.RemoveSpecialCharacter(_document.DocumentNode.SelectSingleNode("//article[contains(@id,'gioi-thieu')]//div[contains(@class,'content')]/p[1]").InnerText);
                        content.Domain = URL_PASGO;
                        content.ReferUrl = url;
                        content.CreateDate = DateTime.Now;

                        //Lưu vào db
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
                        ent.Content = content.Contents;
                        ent.Image = content.ImageThumb;
                        ent.urlAmphtml = "";

                        ent.ContentNoRemoveHtml = ""; // xóa đi khi lưu xuống cho nhẹ

                        string jsonPost = KafkaPreview.ToJson<ArticleDTO_BigData>(ent);
                        KafkaPreview kafka = new KafkaPreview();
                        //await kafka.InsertPost(jsonPost, "crawler-preview-post"); ;
                        #endregion
                        ushort indexLastComment = 0;
                        //Bóc list cmt
                        HtmlNodeCollection divComment = _document.DocumentNode.SelectNodes($"//div[contains(@class,'_3-8y _5nz1')][position()>{indexLastComment}]");
                        if (divComment == null)
                        {
                            break;
                        }
                        if (divComment != null)
                        {
                            foreach (HtmlNode item in divComment)
                            {
                                DTO.CommentDTO commentDTO = new DTO.CommentDTO();
                                commentDTO.Author = item.SelectSingleNode(".//a[contains(@class,' UFICommentActorName')]")?.InnerText;
                                commentDTO.ContentsComment = Common.Utilities.RemoveSpecialCharacter(item.SelectSingleNode(".//span[contains(@class,'_5mdd')]/span")?.InnerText);
                                DateTime postDate = DateTime.Now;
                                string datecomment = item.SelectSingleNode(".//abbr[contains(@class,'UFISutroCommentTimestamp')]").InnerText;
                                if (!string.IsNullOrEmpty(datecomment))
                                {
                                    Common.DateTimeFormatAgain dtFomat = new Common.DateTimeFormatAgain();
                                    string date = dtFomat.FindDateFromText(datecomment);

                                    try
                                    {
                                        postDate = Convert.ToDateTime(date);
                                    }
                                    catch { }
                                }
                                commentDTO.PostDate = postDate;
                                commentDTO.CreateDate = DateTime.Now;
                                commentDTO.Domain = URL_PASGO;
                                commentDTO.ReferUrl = url;
                                commentList.Add(commentDTO);

                                CommentDAO msql2 = new CommentDAO(ConnectionDAO.ConnectionToTableLinkProduct);
                                await msql2.InsertListComment(commentDTO);
                                msql2.Dispose();

                                //Update Status (crawled == 1 )
                                ContentDAO msql1 = new ContentDAO(ConnectionDAO.ConnectionToTableLinkProduct);
                                await msql1.UpdateStatus(url);
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
                                enti.Get_Time_String = commentDTO.CreateDate.ToString("yyyy-MM-dd HH:mm:ss");

                                string jsonPost1 = KafkaPreview.ToJson<ArticleDTO_BigData>(enti);
                                KafkaPreview kafka1 = new KafkaPreview();
                                ///await kafka1.InsertPost(jsonPost1, "crawler-preview-post-comment");
                                #endregion

                                indexLastComment++;
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
