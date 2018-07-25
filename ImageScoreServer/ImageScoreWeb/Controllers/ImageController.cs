using ImageScoreCommon;
using Microsoft.WindowsAzure.ServiceRuntime;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Queue;
using Microsoft.WindowsAzure.Storage.RetryPolicies;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;

namespace ImageScoreWeb.Controllers
{
    public class ImageController : ApiController
    {
        private ImageScoreContext db = new ImageScoreContext();
        private CloudBlobContainer imagesBlobContainer; // image blob container

        public ImageController()
        {
            InitializeStorage(); // initialize 
        }

        public void InitializeStorage()
        {
            // summary
            // get image queue and blob container using credentials from .cscfg file.

            // Open storage account using credentials from .cscfg file.
            var storageAccount = CloudStorageAccount.Parse(RoleEnvironment.GetConfigurationSettingValue("StorageConnectionString"));

            // Get context object for working with blobs, and 
            // set a default retry policy appropriate for a web user interface.
            var blobClient = storageAccount.CreateCloudBlobClient();
            blobClient.DefaultRequestOptions.RetryPolicy = new LinearRetry(TimeSpan.FromSeconds(3), 3);

            // Get a reference to the blob container.
            imagesBlobContainer = blobClient.GetContainerReference("scoreimages");
        }

        [Route("labelrank")]
        [HttpGet]
        public async Task<IEnumerable<ScoreImageReturn>> GetLabelRankImages(int label, int topK, string visitorWechatId)
        {
            List<ScoreImageReturn> returnList = new List<ScoreImageReturn>();
            if (!Enum.IsDefined(typeof(Label), label))
            {
                return returnList;
            }

            var query = await (from img in db.Images // to do top N
                               where img.Label == (Label)label
                               orderby img.Score descending
                               select img).Take(topK).ToListAsync();
            
            foreach (ScoreImage img in query)
            {
                ScoreImageReturn imgR = new ScoreImageReturn(img);
                imgR.isLike = await isLike(visitorWechatId, imgR);
                returnList.Add(imgR);
            }
            
            return returnList;
        }

        [Route("randomimages")]
        [HttpGet]
        public async Task<IEnumerable<ScoreImageReturn>> GetRandomImages(int K, string visitorWechatId)
        {
            var query = await (from img in db.Images
                               orderby Guid.NewGuid()
                               select img).Take(K).ToListAsync();

            List<ScoreImageReturn> returnList = new List<ScoreImageReturn>();
            foreach (ScoreImage img in query)
            {
                ScoreImageReturn imgR = new ScoreImageReturn(img);
                imgR.isLike = await isLike(visitorWechatId, imgR);
                returnList.Add(imgR);
            }

            return returnList;
        }

        [Route("userimages")]
        [HttpGet]
        public async Task<IEnumerable<ScoreImageReturn>> GetUserImages(string visitorWechatId)
        {
            List<ScoreImageReturn> returnList = new List<ScoreImageReturn>();
            if (visitorWechatId == null)
            {
                return returnList;
            }

            var query = await (from img in db.Images
                               where img.WechatId == visitorWechatId
                               orderby img.PostedDate descending
                               select img).ToListAsync();
                        
            foreach (ScoreImage img in query)
            {
                ScoreImageReturn imgR = new ScoreImageReturn(img);
                imgR.isLike = await isLike(visitorWechatId, imgR);
                returnList.Add(imgR);
            }

            return returnList;
        }

        [Route("userlabelimages")]
        [HttpGet]
        public async Task<IEnumerable<ScoreImageReturn>> GetUserLabelImages(string visitorWechatId, int label)
        {
            List<ScoreImageReturn> returnList = new List<ScoreImageReturn>();
            if (!Enum.IsDefined(typeof(Label), label))
            {
                return returnList;
            }

            var query = await (from img in db.Images
                               where (img.WechatId == visitorWechatId && img.Label == (Label)label)
                               orderby img.Score descending
                               select img).ToListAsync();
            
            foreach (ScoreImage img in query)
            {
                ScoreImageReturn imgR = new ScoreImageReturn(img);
                imgR.isLike = await isLike(visitorWechatId, imgR);
                returnList.Add(imgR);
            }

            return returnList;
        }

        public async Task<bool> isLike(string wechatId, ScoreImageReturn imgR)
        {
            var q = await (from like in db.Likes
                           where (like.imageId == imgR.ImageId && like.wechatId == wechatId)
                           select like).ToListAsync();
            return (q.Count > 0);
        }

        [Route("postimage")]
        [HttpPost]
        public async Task<HttpResponseMessage> PostImageTest()
        {
            var response = Request.CreateResponse();
            string message = "successful.";
            bool isWrong = false;

            // Client-provided attributes
            string wechatid = HttpContext.Current.Request["WechatId"];
            string label = HttpContext.Current.Request["Label"];
            string score = HttpContext.Current.Request["Score"];
            string imageHash;
            var imageFile = HttpContext.Current.Request.Files.Count > 0 ? HttpContext.Current.Request.Files[0] : null;

            isWrong = checkInput(wechatid, label, score, imageFile, out message, out imageHash);

            if (isWrong)
            {
                response.StatusCode = HttpStatusCode.Forbidden;
                response.Content = new StringContent(message);
                return response;
            }

            ScoreImage img = new ScoreImage();
            img.WechatId = wechatid;
            img.Score = double.Parse(score);
            img.Label = (Label)int.Parse(label);
            img.imageHash = imageHash;

            CloudBlockBlob imageBlob = null;
            if (ModelState.IsValid)
            {
                imageBlob = await UploadAndSaveBlobAsync(imageFile);
                img.ImageURL = imageBlob.Uri.ToString();
                img.PostedDate = DateTime.Now;

                // generate thumbnail
                generateThumbnail(img);

                db.Images.Add(img);
                await db.SaveChangesAsync();
                Trace.TraceInformation("Created ImageId {0} in database", img.ImageId);
                    
                response.StatusCode = HttpStatusCode.OK;
                message = "successful.";
            }
            else
            {
                response.StatusCode = HttpStatusCode.InternalServerError;
                message = "fail: server error.";
            }
            response.Content = new StringContent(message);
            return response;
        }

        public void generateThumbnail(ScoreImage img)
        {
            Uri blobUri = new Uri(img.ImageURL); // get image url
            string blobName = blobUri.Segments[blobUri.Segments.Length - 1]; // ??? get blobname using image url

            // use blobname to get thumbnial name, also get input blob and output blob
            CloudBlockBlob inputBlob = this.imagesBlobContainer.GetBlockBlobReference(blobName);
            string thumbnailName = Path.GetFileNameWithoutExtension(inputBlob.Name) + "thumb.jpg";
            CloudBlockBlob outputBlob = this.imagesBlobContainer.GetBlockBlobReference(thumbnailName);

            int[] size;
            using (Stream input = inputBlob.OpenRead()) // transform input/output blobs to streams
            using (Stream output = outputBlob.OpenWrite())
            {
                size = ConvertImageToThumbnailJPG(input, output); // call a function to get blob containing thumbnail
                outputBlob.Properties.ContentType = "image/jpeg";
            }
            Trace.TraceInformation("Generated thumbnail in blob {0}", thumbnailName);
            img.ImageWidth = size[0];
            img.ImageHeight = size[1];
            img.ThumbnailWidth = size[2];
            img.ThumbnailHeight = size[3];
            img.ThumbnailURL = outputBlob.Uri.ToString(); // add a item "ThumbnailURL" to advertisement, containing the url of thembnail
            //db.SaveChanges(); // database do saving
        }

        public int[] ConvertImageToThumbnailJPG(Stream input, Stream output) // get the thumbnail of a image
        {
            // summary
            // create a thumbnail for a source image, and save it to output stream

            //var originalImage = new Bitmap(input);
            System.Drawing.Image originalImage = System.Drawing.Image.FromStream(input);

            // compute the height and width of the thumbnail
            int width;
            int height;
            int thumbnailwidth = 500;
            width = originalImage.Width > thumbnailwidth ? thumbnailwidth : originalImage.Width; // min(width, thumbnailwidth)
            height = originalImage.Height * width / originalImage.Width;

            Bitmap thumbnailImage = null;
            try
            {
                thumbnailImage = new Bitmap(width, height); // create a new image, waiting to be drawe
                foreach (PropertyItem p in originalImage.PropertyItems) // remain EXIF properties.
                {
                    thumbnailImage.SetPropertyItem(p);
                }

                using (Graphics graphics = Graphics.FromImage(thumbnailImage))
                {
                    graphics.InterpolationMode = InterpolationMode.HighQualityBicubic; // specify the algorithm when scaling or rotating the image.
                    graphics.SmoothingMode = SmoothingMode.AntiAlias; // whether smoothing is applied
                    graphics.PixelOffsetMode = PixelOffsetMode.HighQuality; // balabala
                    graphics.DrawImage(originalImage, 0, 0, width, height); // draw the image
                }

                thumbnailImage.Save(output, ImageFormat.Jpeg); // save to blob
            }
            finally
            {
                if (thumbnailImage != null)
                {
                    thumbnailImage.Dispose();
                }
            }
            if (thumbnailImage != null)
            {
                int orientationCode = getOrientationCode(originalImage.PropertyItems);
                // return [original.visualWidth, original.visualHeight, thumbnail.visualWidth, thumbnail.visualHeight]
                switch (orientationCode)
                {
                    case 1:
                    case 2:
                    case 3:
                    case 4:
                        {
                            return new int[] { originalImage.Width, originalImage.Height, width, height };
                        }
                    default:
                        {
                            return new int[] { originalImage.Height, originalImage.Width, height, width };
                        }                 
                }
                //return new int[] { originalImage.Width, originalImage.Height, width, height };
            }
            else
            {
                return new int[] { 0, 0, 0, 0 };
            }
        }

        public int getOrientationCode(PropertyItem[] imageProperties)
        {
            foreach (PropertyItem p in imageProperties)
            {
                if (0x0112 == p.Id)
                {
                    return BitConverter.ToInt16(p.Value, 0);
                }
            }
            return -1;
        }

        public bool checkInput(string wechatid, string label, string score, HttpPostedFile imageFile, out string message, out string imageHash)
        {
            bool isWrong = false;
            message = "successful.";
            imageHash = null;

            // Check image file
            if (imageFile == null)
            {
                message = "fail: null image file.";
                isWrong = true;
            }
            else if (imageFile.ContentLength > 10 * 1024 * 1024)
            {
                message = "fail: image file is too large.";
                isWrong = true;
            }
            else if (imageFile.ContentLength == 0)
            {
                message = "fail: image file is empty.";
                isWrong = true;
            }
            else // check whether repeated.
            {
                string itshash;
                var fileStream = imageFile.InputStream;
                fileStream.Position = 0;
                var hash = System.Security.Cryptography.HashAlgorithm.Create();
                itshash = BitConverter.ToString(hash.ComputeHash(fileStream));
                imageHash = itshash;
                var queryRepeated = (from img in db.Images
                                     where img.imageHash == itshash
                                     select img).ToList();
                if (queryRepeated.Count > 0)
                {
                    message = "fail: repeated image file.";
                    isWrong = true;
                }
            }
            // how to check whether image file ?

            // Check attributes
            if (null == wechatid || "" == wechatid)
            {
                message = "fail: WechatId should be specified.";
                isWrong = true;
            }
            if (null == label || "" == label)
            {
                message = "fail: Label should be specified.";
                isWrong = true;
            }
            else
            {
                try
                {
                    int v = int.Parse(label);
                    if (!Enum.IsDefined(typeof(Label), v))
                    {
                        message = "fail: unkown label (expected 0-8).";
                        isWrong = true;
                    }
                }
                catch
                {
                    message = "fail: unkown label (expected 0-8).";
                    isWrong = true;
                }
            }
            if (null == score || "" == score)
            {
                message = "fail: Score should be specified.";
                isWrong = true;
            }
            else
            {
                try
                {
                    double v = double.Parse(score);
                    if (v < 0 || v > 100)
                    {
                        message = "fail: score out of range (expected [0, 100]).";
                        isWrong = true;
                    }
                }
                catch
                {
                    message = "fail: score has wrong format.";
                    isWrong = true;
                }
            }
            return isWrong;
        }

        private async Task<CloudBlockBlob> UploadAndSaveBlobAsync(HttpPostedFile imageFile)
        {
            // summary
            // 1.change the filename to a globally unique filename
            // 2.upload the image to image blob container

            Trace.TraceInformation("Uploading image file {0}", imageFile.FileName);

            // Guid: Rrepresent a globally unique id. That is to say, change the filename of image file.
            string blobName = Guid.NewGuid().ToString() + Path.GetExtension(imageFile.FileName);

            // Retrieve reference to a blob. 
            CloudBlockBlob imageBlob = imagesBlobContainer.GetBlockBlobReference(blobName);
            // Create the blob by uploading a local file.
            var fileStream = imageFile.InputStream;
            fileStream.Position = 0;
            await imageBlob.UploadFromStreamAsync(fileStream);
            
            Trace.TraceInformation("Uploaded image file to {0}", imageBlob.Uri.ToString());

            return imageBlob;
        }

        [Route("like")]
        [HttpPost]
        public async Task<HttpResponseMessage> PostLike()
        {
            var response = Request.CreateResponse();
            string message = "successful.";
            response.Content = new StringContent(message);
            response.StatusCode = HttpStatusCode.OK;

            string imageIdStr = HttpContext.Current.Request["imageId"];
            string visitorWechatId = HttpContext.Current.Request["VisitorWechatId"];
            string isToCancelLikeStr = HttpContext.Current.Request["IsToCancelLike"];

            if (null == visitorWechatId || "" == visitorWechatId)
            {
                message = "visitorWechatId not found or set as null.";
                response.Content = new StringContent(message);
                response.StatusCode = HttpStatusCode.Forbidden;
                return response;
            }

            bool isToCancelLike;
            int imageId;
            try
            {
                imageId = int.Parse(imageIdStr);
                isToCancelLike = bool.Parse(isToCancelLikeStr);
            }
            catch
            {
                message = "IsToCancelLike or imageId has wrong format.";
                response.Content = new StringContent(message);
                response.StatusCode = HttpStatusCode.Forbidden;
                return response;
            }

            var query = await (from like in db.Likes
                               where (like.imageId == imageId && like.wechatId == visitorWechatId)
                               select like).ToListAsync();
            if (isToCancelLike) // to cancel like
            {
                if (query.Count == 0)
                {
                    message = "fail: this image is not liked by this user.";
                    response.Content = new StringContent(message);
                    response.StatusCode = HttpStatusCode.Forbidden;
                    return response;
                }
                ScoreImage img = await db.Images.FindAsync(imageId); // like num --
                img.LikeNum -= 1;
                Like toDeleteLike = query[0];
                db.Likes.Remove(toDeleteLike);
                await db.SaveChangesAsync();
            }
            else // to add like
            {
                if (query.Count > 0)
                {
                    message = "fail: this image is already liked by this user.";
                    response.Content = new StringContent(message);
                    response.StatusCode = HttpStatusCode.Forbidden;
                    return response;
                }

                ScoreImage img = await db.Images.FindAsync(imageId); // like num ++
                img.LikeNum += 1;
                Like newLikeItem = new Like();
                newLikeItem.imageId = imageId; // insert a item of like
                newLikeItem.wechatId = visitorWechatId;
                db.Likes.Add(newLikeItem);
                await db.SaveChangesAsync();
            }
            return response;
        }

        //=========test methods===========

        [Route("getallimages")]
        [HttpGet]
        public async Task<IEnumerable<ScoreImage>> GetAllImages()
        {
            var imgsList = db.Images.AsQueryable();

            int count = imgsList.Count();

            return (await imgsList.ToListAsync());
        }

        [Route("getalllikes")]
        [HttpGet]
        public async Task<IEnumerable<Like>> GetAlllikes()
        {
            var likesList = db.Likes.AsQueryable();

            int count = likesList.Count();

            return (await likesList.ToListAsync());
        }

        [Route("deleteall")]
        [HttpGet]
        public async Task DeleteAll(string password)
        {
            if("areyousure" != password)
            {
                return;
            }
            var imgsList = db.Images.AsQueryable();
            foreach (ScoreImage img in (await imgsList.ToListAsync()))
            {
                db.Images.Remove(img);
            }
            var likesList = db.Likes.AsQueryable();
            foreach (Like like in (await likesList.ToListAsync()))
            {
                db.Likes.Remove(like);
            }
            await db.SaveChangesAsync();
        }

        [Route("deleteoneimage")]
        [HttpGet]
        public async Task DeleteOneImage(int imageId, string password)
        {
            if ("areyousure" != password)
            {
                return;
            }

            var imgs = await (from img in db.Images
                              where img.ImageId == imageId
                              select img).ToListAsync();

            foreach (ScoreImage img in imgs)
            {
                var likes = await (from like in db.Likes
                                   where like.imageId == img.ImageId
                                   select like).ToListAsync();
                foreach (Like l in likes)
                {
                    db.Likes.Remove(l);
                }
                db.Images.Remove(img);
            }
            await db.SaveChangesAsync();
        }

        [Route("deleteoneuserimages")]
        [HttpGet]
        public async Task DeleteOneUserImages(string wechatId, string password)
        {
            if ("areyousure" != password)
            {
                return;
            }

            var imgs = await (from img in db.Images
                               where img.WechatId == wechatId
                               select img).ToListAsync();

            foreach (ScoreImage img in imgs)
            {
                var likes = await (from like in db.Likes
                                   where like.imageId == img.ImageId
                                   select like).ToListAsync();
                foreach (Like l in likes)
                {
                    db.Likes.Remove(l);
                }
                db.Images.Remove(img);
            }
            await db.SaveChangesAsync();
        }

        [Route("testpostfile")]
        [HttpPost]
        public string PostFileTest()
        {
            string name = HttpContext.Current.Request["name"];
            string wechat = HttpContext.Current.Request["Wechat"];

            var file = HttpContext.Current.Request.Files.Count > 0 ?
                HttpContext.Current.Request.Files[0] : null;
            string fileState;
            if (file != null)
            {
                Trace.TraceInformation("Uploading image file {0}", file.FileName);
                fileState = " got file. ";
            }
            else
            {
                Trace.TraceInformation("null image file");
                fileState = " null file. ";
            }

            return name + fileState + wechat;
        }


        [Route("testpostinstance")]
        [HttpPost]
        public string PostInstance(ScoreImage img)
        {
            return img.Label.ToString();
        }

        [Route("geta")]
        [HttpGet]
        public string GetA(int id)
        {
            return "my value a";
        }

        [Route("getb")]
        [HttpGet]
        public string GetB(int id)
        {
            return "my value b";
        }

        [Route("posta")]
        [HttpPost]
        public void PostA([FromBody]string value)
        {
        }

        [Route("postb")]
        [HttpPost]
        public void PostB([FromBody]string value)
        {
        }
    }
}
