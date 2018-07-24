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
        private CloudQueue imagesQueue; // image queue
        private static CloudBlobContainer imagesBlobContainer; // image blob container

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

            // Get context object for working with queues, and 
            // set a default retry policy appropriate for a web user interface.
            CloudQueueClient queueClient = storageAccount.CreateCloudQueueClient();
            queueClient.DefaultRequestOptions.RetryPolicy = new LinearRetry(TimeSpan.FromSeconds(3), 3);

            // Get a reference to the queue.
            imagesQueue = queueClient.GetQueueReference("scoreimages");
        }

        [Route("labelrank")]
        [HttpGet]
        public async Task<IEnumerable<ScoreImage>> GetLabelRankImages(int label, int topK, string visitorWechatId)
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
        public async Task<IEnumerable<ScoreImage>> GetRandomImages(int K, string visitorWechatId)
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
            var imageFile = HttpContext.Current.Request.Files.Count > 0 ? HttpContext.Current.Request.Files[0] : null;

            isWrong = checkInput(wechatid, label, score, imageFile, out message);

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

            CloudBlockBlob imageBlob = null;
            if (ModelState.IsValid)
            {
                imageBlob = await UploadAndSaveBlobAsync(imageFile);
                img.ImageURL = imageBlob.Uri.ToString();

                img.PostedDate = DateTime.Now;
                db.Images.Add(img);
                await db.SaveChangesAsync();
                Trace.TraceInformation("Created ImageId {0} in database", img.ImageId);

                var queueMessage = new CloudQueueMessage(img.ImageId.ToString());
                await imagesQueue.AddMessageAsync(queueMessage);
                Trace.TraceInformation("Created queue message for AdId {0}", img.ImageId);
                    
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

        public bool checkInput(string wechatid, string label, string score, HttpPostedFile imageFile, out string message)
        {
            bool isWrong = false;
            message = "successful.";

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
            using (var fileStream = imageFile.InputStream)
            {
                await imageBlob.UploadFromStreamAsync(fileStream);
            }

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
        public async Task DeleteAll()
        {
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

        [Route("deleteoneuserimages")]
        [HttpGet]
        public async Task DeleteOneUserImages(string wechatId)
        {
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
