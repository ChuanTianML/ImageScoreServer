using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ImageScoreCommon
{
    public enum Label
    {
        animal,
        architecture,
        cityscape,
        floral,
        fooddrink,
        landscape,
        portrait,
        stilllife,
        others
    }

    public class ScoreImage
    {
        [Key]
        public int ImageId { get; set; }

        public string imageHash { get; set; }

        public string WechatId { get; set; }

        public DateTime PostedDate { get; set; }

        public Label Label { get; set; }

        public double Score { get; set; }

        public int LikeNum { get; set; }

        public int DislikeNum { get; set; }

        //======== Source Image =======//

        [StringLength(2083)]
        public string ImageURL { get; set; }

        public int ImageWidth { get; set; }

        public int ImageHeight { get; set; }


        // ======== Thumbnail ==========//

        [StringLength(2083)]
        public string ThumbnailURL { get; set; }

        public int ThumbnailWidth { get; set; }

        public int ThumbnailHeight { get; set; }
    }
}
