using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ImageScoreCommon
{
    public class ScoreImageReturn
    {
        public bool isLike { get; set; }

        public int ImageId { get; set; }

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

        public ScoreImageReturn()
        {
        }

        public ScoreImageReturn(ScoreImage si)
        {
            this.ImageId = si.ImageId;
            this.WechatId = si.WechatId;
            this.PostedDate = si.PostedDate;
            this.Label = si.Label;
            this.Score = si.Score;
            this.LikeNum = si.LikeNum;
            this.DislikeNum = si.DislikeNum;
            //======== Source Image =======//
            this.ImageURL = si.ImageURL;
            this.ImageWidth = si.ImageWidth;
            this.ImageHeight = si.ImageHeight;
            // ======== Thumbnail ==========//
            this.ThumbnailURL = si.ThumbnailURL;
            this.ThumbnailWidth = si.ThumbnailWidth;
            this.ThumbnailHeight = si.ThumbnailHeight;
        }
    }
}
