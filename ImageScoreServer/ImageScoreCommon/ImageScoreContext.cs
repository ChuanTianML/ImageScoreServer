using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ImageScoreCommon
{
    public class ImageScoreContext : DbContext
    {
        public ImageScoreContext() : base("name=ImageScoreContext") //
        {
        }

        public ImageScoreContext(string connString) : base(connString)
        {
        }

        public DbSet<ScoreImage> Images { get; set; }
    }
}
