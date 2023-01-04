using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading.Tasks;
using VCCorp.CrawlerPreview.DTO;

namespace VCCorp.CrawlerPreview.DAO
{
    public class ContentDAO
    {
        private readonly MySqlConnection _conn;
        public ContentDAO(string connection)
        {
            _conn = new MySqlConnection(connection);
        }

        public void Dispose()
        {
            Dispose(true);
        }

        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_conn.State == System.Data.ConnectionState.Open)
                {
                    _conn.Close();
                    _conn.Dispose();
                }
                else
                {
                    _conn.Dispose();
                }
            }
        }

        /// <summary>
        /// Insert URL content 
        /// </summary>
        /// <param name="content"></param>
        /// <returns></returns>
        public async Task<int> InsertLinkContent(ContentDTO content)
        {
            int res = 0;

            try
            {
                await _conn.OpenAsync();

                string query = "insert ignore into crawler_preview.list_url (Url,CreateDate,Domain,Status) values (@Url,@CreateDate,@Domain,@Status)";

                MySqlCommand cmd = new MySqlCommand();
                cmd.Connection = _conn;
                cmd.CommandText = query;

                cmd.Parameters.AddWithValue("@Url", content.ReferUrl);
                cmd.Parameters.AddWithValue("@CreateDate", content.CreateDate);
                cmd.Parameters.AddWithValue("@Domain", content.Domain);
                cmd.Parameters.AddWithValue("@Status", content.Status);

                await cmd.ExecuteNonQueryAsync();

                res = 1;

            }
            catch (Exception ex)
            {
                if (ex.Message.ToLower().Contains("duplicate entry"))
                {
                    res = -2; // trùng link
                }
                else
                {
                    res = -1; // lỗi, bắt lỗi trả ra để sửa

                    // ghi lỗi xuống fil
                }
            }

            return res;
        }

        /// <summary>
        /// Insert Content
        /// </summary>
        /// <param name="content"></param>
        /// <returns></returns>
        public async Task<int> InserContent(ContentDTO content)
        {
            int res = 0;

            try
            {
                await _conn.OpenAsync();

                string query = "insert ignore into crawler_preview.content_detail (Subject,TotalPoint,ImageThumb,Summary,Contents,Domain,ReferUrl,CreateDate) " +
                    "values (@Subject,@TotalPoint,@ImageThumb,@Summary,@Contents,@Domain,@ReferUrl,@CreateDate)";

                MySqlCommand cmd = new MySqlCommand();
                cmd.Connection = _conn;
                cmd.CommandText = query;

                cmd.Parameters.AddWithValue("@Subject", content.Subject);
                cmd.Parameters.AddWithValue("@TotalPoint", content.TotalPoint);
                cmd.Parameters.AddWithValue("@ImageThumb", content?.ImageThumb);
                cmd.Parameters.AddWithValue("@Summary", content.Summary);
                cmd.Parameters.AddWithValue("@Contents", content.Contents);
                cmd.Parameters.AddWithValue("@Domain", content.Domain);
                cmd.Parameters.AddWithValue("@ReferUrl", content.ReferUrl);
                cmd.Parameters.AddWithValue("@CreateDate", content.CreateDate);

                await cmd.ExecuteNonQueryAsync();

                res = 1;

            }
            catch (Exception ex)
            {
                if (ex.Message.ToLower().Contains("duplicate entry"))
                {
                    res = -2; // trùng link
                }
                else
                {
                    res = -1; // lỗi, bắt lỗi trả ra để sửa

                    // ghi lỗi xuống fil
                }
            }

            return res;
        }

        /// <summary>
        /// Select URL
        /// </summary>
        /// <param name="content"></param>
        /// <returns></returns>
        public List<ContentDTO> GetLinkByDomain(string domain)
        {
            List<ContentDTO> data = new List<ContentDTO>();
            string query = $"Select * from crawler_preview.list_url where Domain ='{domain}'";
            try
            {
                using (MySqlCommand cmd = new MySqlCommand(query, _conn))
                {
                    _conn.Open();
                    using (DbDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            data.Add(new ContentDTO
                            {
                                ReferUrl = reader["Url"].ToString(),
                                Status = (int)reader["Status"]
                            }
                            );
                        }
                    }


                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            _conn.Close();

            return data;
        }

        /// <summary>
        /// Update status
        /// </summary>
        /// <param name="content"></param>
        /// <returns></returns>
        public async Task<int> UpdateStatus(string url)
        {
            ContentDTO content = new ContentDTO();
            int res = 0;
            try
            {
                await _conn.OpenAsync();

                string query = $"UPDATE crawler_preview.list_url SET Status = 1 WHERE Url = '{url}'";

                MySqlCommand cmd = new MySqlCommand();
                cmd.Connection = _conn;
                cmd.CommandText = query;
                cmd.Parameters.AddWithValue("@Status", content.Status);
                await cmd.ExecuteNonQueryAsync();

                res = 1;


            }
            catch (Exception ex)
            {
                if (ex.Message.ToLower().Contains("duplicate entry"))
                {
                    res = -2; // trùng link
                }
                else
                {
                    res = -1; // lỗi, bắt lỗi trả ra để sửa

                    // ghi lỗi xuống fil
                }
            }

            return res;
        }
    } 
}
