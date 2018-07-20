using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ImageScoreCommon
{
    public class Like
    {
        [Key]
        public int likeId { get; set; }

        public int imageId { get; set; }

        public string wechatId { get; set; }
    }
}
