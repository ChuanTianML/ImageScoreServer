using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ImageScoreCommon
{
    public class ScoreImageReturn : ScoreImage
    {
        public bool isLike { get; set; }

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
