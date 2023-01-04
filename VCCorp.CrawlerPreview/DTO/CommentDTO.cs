using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VCCorp.CrawlerPreview.DTO
{
    public class CommentDTO
    {
        public string Id { get; set; }
        public string Point { get; set; } //điểm đánh giá 
        public string ContentsComment { get; set; } // nội dung bình luận
        public string Author { get; set; }//tách giả bình luận
        public DateTime PostDate { get; set; } // ngày đăng comments
        public double PostDateTimeStamp { get; set; } //ngày đăng comments chuyển đổi sang Timestamp
        public DateTime CreateDate { get; set; }//ngày bóc data
        public string Domain { get; set; }// domain trang web
        public string ReferUrl { get; set; }// Url bóc comment
    }
}
