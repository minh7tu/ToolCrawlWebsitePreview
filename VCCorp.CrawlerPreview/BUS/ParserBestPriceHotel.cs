using CefSharp.WinForms;
using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using VCCorp.CrawlerCore.DTO;
using VCCorp.CrawlerPreview.DAO;
using VCCorp.CrawlerPreview.DTO;

namespace VCCorp.CrawlerPreview.BUS
{
    public class ParserBestPriceHotel
    {
        /// <summary>
        /// Lấy danh sách bài viết
        /// </summary>
        /// <param name="html"></param>
        /// <returns></returns>
        public async Task<List<ContentDTO>> GetListContents(string html)
        {
            try
            {
                //Lấy tất cả bài đăng ở trang chủ trang
                string zone = Common.Utilities.Xpath_GetHTML(html, "//ul[contains(@class,'deal-item')]", false);

                // lấy danh sách bài viết trong vùng mới giới hạn zone ở trên
                List<string> listHtmlContent = Common.Utilities.Xpath_GetListHTML(zone, "//li[contains(@class,'hotel-ids')]");

                // duyệt từng bài lấy ra Tiêu đề, hình thumb  (nếu có), url chi tiết, mô tả  (nếu có) và ngày tháng (nếu có)
                if (listHtmlContent != null && listHtmlContent.Count > 0)
                {
                    List<ContentDTO> lisToReturn = new List<ContentDTO>();
                    foreach (string contentHtml in listHtmlContent)
                    {
                        string subject = Common.Utilities.Xpath_GetHTML(contentHtml, "//h3", true); // truyền vào 1 đoạn xpath, xử lý xong trả ra đoạn text thỏa mã đoạn xpath đó, giá trị true để xóa đi các thẻ html giữ lại đoạn text

                        string url = Common.Utilities.Xpath_GetHTML(contentHtml, "//li[contains(@class,'hotel-ids')]", false);
                        url = Common.Utilities.Regex_GetTextFromHtml(url, "(data-go-url[\\s]{0,}=[\\s]{0,}('|\")).*?('|\")", "", "(data-go-url[\\s]{0,}=[\\s]{0,}('|\"))|('|\")", true);
                        if (string.IsNullOrEmpty(url))
                        {
                            url = Common.Utilities.Regex_GetTextFromHtml(url, "data-go-url=(.*?)(.html|.htm)", "", "data-go-url=", true);
                        }

                        // trang này không có đăng ngày tháng ở phần chuyên mục - mình sẽ bỏ qua

                        ContentDTO obj = new ContentDTO();

                        obj.ReferUrl = url;
                        obj.Domain = "https://www.bestprice.vn/";
                        obj.CreateDate = DateTime.Now;
                        obj.Status = 0;

                        lisToReturn.Add(obj);

                        ContentDAO msql = new ContentDAO(ConnectionDAO.ConnectionToTableLinkProduct);
                        await msql.InsertLinkContent(obj);
                        msql.Dispose();
                    }
                    // lấy xong trả ra danh sách thôi
                    return lisToReturn;
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Lấy chi tiết từng bài viết
        /// </summary>
        /// <param name="html"></param>
        /// <returns></returns>
        public async Task<List<ContentDTO>> GetContentsDetail()
        {
            List<ContentDTO> contentList = new List<ContentDTO>();

            try
            {
                ContentDAO contentDAO = new ContentDAO(ConnectionDAO.ConnectionToTableLinkProduct);
                List<ContentDTO> dataUrl = contentDAO.GetLinkByDomain("https://www.bestprice.vn/");
                contentDAO.Dispose();
                for (int i = 0; i < dataUrl.Count; i++)
                {
                    int status = dataUrl[i].Status;
                    if (status == 0)// check xem đã bóc hay chưa?
                    {
                        string url = "https://www.bestprice.vn" + dataUrl[i].ReferUrl;
                        string html = Common.DownloadHtml.GetContentHtml(url);

                        string subject = Common.Utilities.Xpath_GetHTML(html, "//div[contains(@class,'container')]//h1", true); // truyền vào 1 đoạn xpath, xử lý xong trả ra đoạn text thỏa mã đoạn xpath đó, giá trị true để xóa đi các thẻ html giữ lại đoạn text             

                        string content = Common.Utilities.Xpath_GetHTML(html, "//div[contains(@class, 'col-xs-12 col-sm-8')]//p", true);

                        string totalPoint = Common.Utilities.Xpath_GetHTML(html, "//div[contains(@class, 'review-number')]/span", true);

                        string contentHtml = Common.Utilities.Xpath_GetHTML(html, "//div[contains(@class, 'photo-grid')]", false); // nội dung không xóa thẻ Html

                        string imageThumb = "";
                        // trong nội dung bài viết có nhiều hình, cần tìm hình đầu tiên ra làm hình đại diện
                        List<string> lisImg = Common.Utilities.Xpath_GetListHTML(contentHtml, "//img");
                        if (lisImg != null && lisImg.Count > 0)
                        {
                            foreach (string imgHtml in lisImg)
                            {
                                string imageThumbHtml = Common.Utilities.Regex_GetTextFromHtml(imgHtml, "src[\\s]{0,}=[\\s]{0,}\".*?\"", "", "src[\\s]{0,}=[\\s]{0,}\"|\"", true);

                                // sau khi lấy url chỉ là tương đối, để rõ ràng cần nối thêm cái đầu https://www.bestprice.vn/ vào nữa
                                imageThumbHtml = Common.Utilities.FixPathImage(imageThumbHtml, "https://www.bestprice.vn//khach-san/");
                                if (imageThumbHtml.ToLower().Contains(".jpg") || imageThumbHtml.ToLower().Contains(".jpeg") || imageThumbHtml.ToLower().Contains(".png") || imageThumbHtml.ToLower().Contains(".gif"))
                                {
                                    imageThumb = imageThumbHtml;
                                    break;
                                }
                            }
                        }
                        ContentDTO objReturn = new ContentDTO();
                        objReturn.TotalPoint = totalPoint; //lấy tổng điểm 
                        objReturn.ImageThumb = imageThumb; // hình đại diện của bài, thường tách trong nội dung bài viết, lấy hình đầu tiên
                        objReturn.Subject = subject; // tiêu đề tin
                        objReturn.Contents = content; // nội dung bài viết                
                        objReturn.Domain = "https://www.bestprice.vn/";
                        objReturn.ReferUrl = url;
                        objReturn.CreateDate = DateTime.Now;
                        contentList.Add(objReturn);

                        ContentDAO msql = new ContentDAO(ConnectionDAO.ConnectionToTableLinkProduct);
                        await msql.InserContent(objReturn);
                        msql.Dispose();
                        #region gửi đi cho ILS

                        ArticleDTO_BigData ent = new ArticleDTO_BigData();

                        ent.Id = Common.Utilities.Md5Encode(objReturn.Id);
                        ent.Content = objReturn.Contents;

                        //Get_Time là thời gian bóc 
                        ent.Get_Time = objReturn.CreateDate;
                        ent.Get_Time_String = objReturn.CreateDate.ToString("yyyy-MM-dd HH:mm:ss");

                        ent.Title = objReturn.Subject;
                        ent.Url = objReturn.ReferUrl;
                        ent.Source_Id = 0;
                        ent.Image = objReturn.ImageThumb;
                        ent.urlAmphtml = "";

                        ent.ContentNoRemoveHtml = ""; // xóa đi khi lưu xuống cho nhẹ

                        string jsonPost = KafkaPreview.ToJson<ArticleDTO_BigData>(ent);
                        KafkaPreview kafka = new KafkaPreview();
                        await kafka.InsertPost(jsonPost, "crawler-preview-post");
                        #endregion

                    }
                }
            }
            catch { }
            return contentList;
        }

        private ChromiumWebBrowser _browser = null;
        private readonly HtmlAgilityPack.HtmlDocument _document = new HtmlAgilityPack.HtmlDocument();
        private const string _jsClickShowMoreReview = @"document.getElementsByClassName('btn-more-review')[0].click()";

        public ParserBestPriceHotel()
        {
        }
        public ParserBestPriceHotel(ChromiumWebBrowser browser)
        {
            _browser = browser;
        }

        /// <summary>
        /// Bóc chi tiết comment
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        public async Task<List<DTO.CommentDTO>> GetListComment()
        {
            List<DTO.CommentDTO> commentList = new List<DTO.CommentDTO>();
            try
            {
                ContentDAO contentDAO = new ContentDAO(ConnectionDAO.ConnectionToTableLinkProduct);
                List<ContentDTO> dataUrl = contentDAO.GetLinkByDomain("https://www.bestprice.vn/");
                contentDAO.Dispose();
                for (int i = 0; i < dataUrl.Count; i++)
                {
                    int status = dataUrl[i].Status;
                    if (status == 0)// check xem đã bóc hay chưa?
                    {
                        string url = "https://www.bestprice.vn" + dataUrl[i].ReferUrl;
                        string referUrl = dataUrl[i].ReferUrl;
                        await _browser.LoadUrlAsync(url);
                        await Task.Delay(10_000);
                        ushort indexLastComment = 0;
                        while (true)
                        {
                            string html = await Common.Utilities.GetBrowserSource(_browser).ConfigureAwait(false);
                            _document.LoadHtml(html);
                            html = null;

                            HtmlNodeCollection divComment = _document.DocumentNode.SelectNodes($"//div[@class='rv-content']//div[contains(@class,'item-rv')][position()>{indexLastComment}]");
                            if (divComment == null)
                            {
                                break;
                            }
                            if (divComment != null)
                            {
                                foreach (HtmlNode item in divComment)
                                {
                                    DTO.CommentDTO comment = new DTO.CommentDTO();
                                    comment.Point = item.SelectSingleNode(".//div[contains(@class,'rv-name')]").InnerText;
                                    comment.Author = item.SelectSingleNode(".//p[contains(@class,'cus-rv-name')]/span[2]")?.InnerText;
                                    comment.ContentsComment = Common.Utilities.RemoveSpecialCharacter(item.SelectSingleNode(".//div[contains(@class,'cus-rv-content')]/span")?.InnerText);
                                    if (!String.IsNullOrEmpty(comment.Author) && !String.IsNullOrEmpty(comment.ContentsComment))
                                    {
                                        comment.ReferUrl = url;
                                        comment.Domain = "https://www.bestprice.vn/";

                                        DateTime postDate = DateTime.Now;
                                        string datecomment = item.SelectSingleNode(".//p[contains(@class,'cus-rv-date')]").InnerText;
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
                                        comment.PostDate = postDate;
                                        comment.CreateDate = DateTime.Now;
                                        commentList.Add(comment);

                                        CommentDAO msql = new CommentDAO(ConnectionDAO.ConnectionToTableLinkProduct);
                                        await msql.InsertListComment(comment);
                                        msql.Dispose();

                                        ContentDAO msql1 = new ContentDAO(ConnectionDAO.ConnectionToTableLinkProduct);
                                        await msql1.UpdateStatus(referUrl);
                                        msql1.Dispose();

                                        #region gửi đi cho ILS

                                        ArticleDTO_BigData ent = new ArticleDTO_BigData();

                                        ent.Comment = comment.ContentsComment;
                                        ent.Author = comment.Author;
                                        // thời gian tạo tin
                                        ent.Create_time = comment.PostDate;
                                        ent.Create_Time_String = comment.PostDate.ToString("yyyy-MM-dd HH:mm:ss");

                                        //Get_Time là thời gian bóc 
                                        ent.Get_Time = comment.CreateDate;
                                        ent.Get_Time_String = comment.CreateDate.ToString("yyyy-MM-dd HH:mm:ss");

                                        ent.Url = comment.ReferUrl;

                                        string jsonPost1 = KafkaPreview.ToJson<ArticleDTO_BigData>(ent);
                                        KafkaPreview kafka1 = new KafkaPreview();
                                        await kafka1.InsertPost(jsonPost1, "crawler-preview-post-comment");
                                        #endregion
                                    }
                                    indexLastComment++;
                                }
                            }
                            HtmlNode checkExitButtonLoadMore = _document.DocumentNode.SelectSingleNode("//button[contains(@class,'btn-more-review')]");
                            if (checkExitButtonLoadMore == null)
                            {
                                break;
                            }

                            /* Check end page(if end page => data return = null because not found div button show more) and check error js */
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
