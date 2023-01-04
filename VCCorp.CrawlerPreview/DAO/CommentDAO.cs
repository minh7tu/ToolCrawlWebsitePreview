using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VCCorp.CrawlerPreview.DTO;

namespace VCCorp.CrawlerPreview.DAO
{
    public class CommentDAO
    {
        private readonly MySqlConnection _conn;
        public CommentDAO(string connection)
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
        /// Insert URL comment 
        /// </summary>
        /// <param name="content"></param>
        /// <returns></returns>
        public async Task<int> InsertListComment(CommentDTO comment)
        {
            int res = 0;

            try
            {
                await _conn.OpenAsync();

                string query = "insert ignore into crawler_preview.comment_detail (Point,ContentsComment,Author,PostDate,CreateDate,Domain,ReferUrl) " +
                    "values (@Point,@ContentsComment,@Author,@PostDate,@CreateDate,@Domain,@ReferUrl)";

                MySqlCommand cmd = new MySqlCommand();
                cmd.Connection = _conn;
                cmd.CommandText = query;

                cmd.Parameters.AddWithValue("@Point", comment.Point);
                cmd.Parameters.AddWithValue("@ContentsComment", comment.ContentsComment);
                cmd.Parameters.AddWithValue("@Author", comment.Author);
                cmd.Parameters.AddWithValue("@PostDate", comment.PostDate);
                cmd.Parameters.AddWithValue("@CreateDate", comment.CreateDate);
                cmd.Parameters.AddWithValue("@Domain", comment.Domain);
                cmd.Parameters.AddWithValue("@ReferUrl", comment.ReferUrl);


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
