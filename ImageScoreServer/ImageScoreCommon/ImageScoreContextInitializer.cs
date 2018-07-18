using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ImageScoreCommon
{
    //class ImageScoreContextInitializer : System.Data.Entity.DropCreateDatabaseAlways<ImageScoreContext>
    class ImageScoreContextInitializer : System.Data.Entity.DropCreateDatabaseIfModelChanges<ImageScoreContext>
    {
        protected override void Seed(ImageScoreContext context)
        {
            var imgs = new List<ScoreImage>
            {

            };
            imgs.ForEach(s => context.Images.Add(s));
            context.SaveChanges();
        }
    }
}
