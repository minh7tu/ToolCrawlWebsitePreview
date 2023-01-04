﻿using CefSharp.WinForms;
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
    public class ParserMyTour
    {
        private ChromiumWebBrowser _browser = null;
        private readonly HtmlAgilityPack.HtmlDocument _document = new HtmlAgilityPack.HtmlDocument();
        private const string _jsClickShowMoreReview = @"document.getElementsByClassName('MuiButtonBase-root MuiButton-root MuiButton-text jss243')[0].click()";
        private const string _jsAutoScroll = @"function pageScroll() {window.scrollBy(0,10);scrolldelay = setTimeout(pageScroll,30);}{window.scrollBy(0,10);scrolldelay = setTimeout(pageScroll,30);}";
        private string URL_MYTOUR = "https://mytour.vn";
        string URL_MYTOUR_HOTEL_DN = "https://mytour.vn/khach-san/tp50/khach-san-tai-da-nang.html";

        string URL_MYTOUR_HOTEL_HCM = "https://mytour.vn/khach-san/tp33/khach-san-tai-ho-chi-minh.html";

        string URL_MYTOUR_HOTEL_HN = "https://mytour.vn/khach-san/tp11/khach-san-tai-ha-noi.html";

        public ParserMyTour(ChromiumWebBrowser browser)
        {
            _browser = browser;
        }

        public async Task CrawlData()
        {
            await GetListHotel(URL_MYTOUR_HOTEL_HCM);
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
                await Task.Delay(40_000);

                while (true)
                {
                    string html = await Common.Utilities.GetBrowserSource(_browser).ConfigureAwait(false);
                    _document.LoadHtml(html);
                    html = null;

                    HtmlNodeCollection divComment = _document.DocumentNode.SelectNodes($"//div[contains(@class,'item-hotel-listing')][position()>{indexLastContent}]");
                    if (divComment == null)
                    {
                        break;
                    }

                    if (divComment != null)
                    {
                        foreach (HtmlNode item in divComment)
                        {
                            string listURL = item.SelectSingleNode(".//a[contains(@class,'MuiLink-root')]")?.Attributes["href"]?.Value ?? "";
                            //loại bỏ kí tự đằng sau dấu '?' chỉ lấy id hotel
                            listURL = Regex.Replace(listURL, @"\?[\s\S]+", " ", RegexOptions.IgnoreCase);
                            ContentDTO content = new ContentDTO();
                            content.ReferUrl = listURL;
                            content.CreateDate = DateTime.Now; // ngày bóc tách
                            content.CreateDate_Timestamp = Common.Utilities.DateTimeToUnixTimestamp(DateTime.Now); // ngày bóc tách chuyển sang dạng Timestamp
                            content.Domain = URL_MYTOUR;
                            content.Status = 0;
                            contentList.Add(content);

                            ContentDAO msql = new ContentDAO(ConnectionDAO.ConnectionToTableLinkProduct);
                            await msql.InsertLinkContent(content);
                            msql.Dispose();

                            indexLastContent++;
                        }
                    }
                    //string checkJs = await Common.Utilities.EvaluateJavaScriptSync(_jsAutoScroll, _browser).ConfigureAwait(false);
                    //if (checkJs == null)
                    //{
                    //    break;
                    //}
                    //await Task.Delay(15_000);

                    //string checkJs1 = await Common.Utilities.EvaluateJavaScriptSync(_jsClickShowMoreReview, _browser).ConfigureAwait(false);
                    //if (checkJs == null)
                    //{
                    //    break;
                    //}

                    //await Task.Delay(10_000);
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
        public async Task<List<DTO.CommentDTO>> GetHotelDetail()
        {
            List<DTO.CommentDTO> commentList = new List<DTO.CommentDTO>();
            try
            {
                //Lấy url hotel từ db
                ContentDAO contentDAO = new ContentDAO(ConnectionDAO.ConnectionToTableLinkProduct);
                List<ContentDTO> dataUrl = contentDAO.GetLinkByDomain(URL_MYTOUR);
                contentDAO.Dispose();

                //Đọc từng url để bóc
                for (int i = 0; i < dataUrl.Count; i++)
                {
                    int status = dataUrl[i].Status;
                    if (status == 0)// check xem đã bóc hay chưa?
                    {
                        string url = URL_MYTOUR + dataUrl[i].ReferUrl;
                        string referUrl = dataUrl[i].ReferUrl;
                        await _browser.LoadUrlAsync(url);
                        await Task.Delay(10_000);

                        string html = await Common.Utilities.GetBrowserSource(_browser).ConfigureAwait(false);
                        _document.LoadHtml(html);
                        html = null;

                        //Bóc hotel detail
                        ContentDTO content = new ContentDTO();
                        content.Subject = _document.DocumentNode.SelectSingleNode("//div[(@id='rooms_overview')]//h1")?.InnerText;
                        content.ImageThumb = _document.DocumentNode.SelectSingleNode("//div[contains(@id,'id-hotel-detail')]//img")?.Attributes["src"]?.Value ?? "";
                        content.Domain = URL_MYTOUR;
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
                        ent.Image = content.ImageThumb;
                        ent.urlAmphtml = "";

                        ent.ContentNoRemoveHtml = ""; // xóa đi khi lưu xuống cho nhẹ

                        string jsonPost = KafkaPreview.ToJson<ArticleDTO_BigData>(ent);
                        KafkaPreview kafka = new KafkaPreview();
                        await kafka.InsertPost(jsonPost, "crawler-preview-post");;
                        #endregion

                        //Bóc list cmt
                        HtmlNodeCollection divComment = _document.DocumentNode.SelectNodes("//div[contains(@class,'jss2')]//div[contains(@class,'MuiGrid-container')]");
                        HtmlNode checkBtnLoadMore = _document.DocumentNode.SelectSingleNode("//div[contains(@id,'evaluate')]//div[3]//div[8]/div");

                        if (divComment != null)
                        {
                            foreach (HtmlNode item in divComment)
                            {
                                DTO.CommentDTO commentDTO = new DTO.CommentDTO();
                                commentDTO.Author = item.SelectSingleNode("./div/div/div[2]/span")?.InnerText;
                                commentDTO.Point = item.SelectSingleNode("./div[2]/div[1]/div[1]/p")?.InnerText;
                                commentDTO.ContentsComment = Common.Utilities.RemoveSpecialCharacter(item.SelectSingleNode("./div[2]/div[1]/div[2]/div[1]")?.InnerText);
                                DateTime postDate = DateTime.Now;
                                string datecomment = item.SelectSingleNode("./div/div/div[2]/div/div[1]/span").InnerText;
                                if (!string.IsNullOrEmpty(datecomment))
                                {
                                    Common.DateTimeFormatAgain dtFomat = new Common.DateTimeFormatAgain();
                                    string date = dtFomat.GetDate(datecomment, "dd/MM/yyyy");

                                    string fulldate = date;

                                    try
                                    {
                                        postDate = Convert.ToDateTime(fulldate);
                                    }
                                    catch { }
                                }
                                commentDTO.PostDate = postDate;
                                commentDTO.CreateDate = DateTime.Now;
                                commentDTO.Domain = URL_MYTOUR;
                                commentDTO.ReferUrl = url;

                                commentList.Add(commentDTO);
                                CommentDAO msql2 = new CommentDAO(ConnectionDAO.ConnectionToTableLinkProduct);
                                await msql2.InsertListComment(commentDTO);
                                msql2.Dispose();

                                //Update Status (crawled == 1 )
                                ContentDAO msql1 = new ContentDAO(ConnectionDAO.ConnectionToTableLinkProduct);
                                await msql1.UpdateStatus(referUrl);
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
                                await kafka1.InsertPost(jsonPost1, "crawler-preview-post-comment");
                                #endregion
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
