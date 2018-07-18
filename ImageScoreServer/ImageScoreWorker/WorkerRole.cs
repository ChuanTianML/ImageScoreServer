using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using ImageScoreCommon;
using Microsoft.Azure;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.Diagnostics;
using Microsoft.WindowsAzure.ServiceRuntime;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Queue;

namespace ImageScoreWorker
{
    public class WorkerRole : RoleEntryPoint
    {
        private CloudQueue imagesQueue;
        private CloudBlobContainer imagesBlobContainer;
        private ImageScoreContext db; // ???

        public override void Run()
        {
            Trace.TraceInformation("Worker entry point called");
            CloudQueueMessage msg = null;
            while (true) // always cycling
            {
                try
                {
                    // Retrieve a new message from the queue.
                    // A production app could be more efficient and scalable and conserve
                    // on transaction costs by using the GetMessages method to get
                    // multiple queue messages at a time. See:
                    // http://azure.microsoft.com/en-us/documentation/articles/cloud-services-dotnet-multi-tier-app-storage-5-worker-role-b/#addcode
                    msg = this.imagesQueue.GetMessage(); // pick a message from image queue, but what is the content ???
                    // GetMessage() does not delete this message.
                    if (msg != null)
                    {
                        ProcessQueueMessage(msg);
                    }
                    else // why sleep for 1 second if is message is null ???
                    {
                        System.Threading.Thread.Sleep(1000);
                    }
                }
                catch (StorageException e) // fail to pick a message from the image queue
                {
                    if (msg != null && msg.DequeueCount > 5) // if this message has been dequeued for more than 5 times, delete it; else, just give a error information
                    {
                        this.imagesQueue.DeleteMessage(msg);
                        Trace.TraceError("Deleting poison queue item: '{0}'", msg.AsString);
                    }
                    Trace.TraceError("Exception in Worker: '{0}'", e.Message);
                    System.Threading.Thread.Sleep(5000);
                }
            }
        }

        private void ProcessQueueMessage(CloudQueueMessage msg) // process a message
        {
            // summary
            // 1.create a thumbnail for an advertisement, save it in image blob container
            // 2.add the url of thumbnail to ad in the database

            Trace.TraceInformation("Processing queue message {0}", msg);

            // Queue message contains AdId.
            var imgId = int.Parse(msg.AsString); // get advertisement id from message
            ScoreImage img = db.Images.Find(imgId); // get this advertisement according to id
            if (img == null) // something wrong, there is no advertisement for this id
            {
                throw new Exception(String.Format("ImageId {0} not found, can't create thumbnail", imgId.ToString()));
            }

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
            img.ImageWidth = size[0];
            img.ImageHeight = size[1];
            img.ThumbnailWidth = size[2];
            img.ThumbnailHeight = size[3];
            Trace.TraceInformation("Generated thumbnail in blob {0}", thumbnailName);

            img.ThumbnailURL = outputBlob.Uri.ToString(); // add a item "ThumbnailURL" to advertisement, containing the url of thembnail
            db.SaveChanges(); // database do saving
            Trace.TraceInformation("Updated thumbnail URL in database: {0}", img.ThumbnailURL);

            // Remove message from queue.
            this.imagesQueue.DeleteMessage(msg); // finally, delete this message from queue
        }


        public int[] ConvertImageToThumbnailJPG(Stream input, Stream output) // get the thumbnail of a image
        {
            // summary
            // create a thumbnail for a source image, and save it to output stream

            int thumbnailsize = 80; // after converting, max(height, width) == 80
            int width;
            int height;
            var originalImage = new Bitmap(input);

            // compute the height and width of the thumbnail
            if (originalImage.Width > originalImage.Height)
            {
                width = thumbnailsize;
                height = thumbnailsize * originalImage.Height / originalImage.Width;
            }
            else
            {
                height = thumbnailsize;
                width = thumbnailsize * originalImage.Width / originalImage.Height;
            }

            Bitmap thumbnailImage = null;
            try
            {
                thumbnailImage = new Bitmap(width, height); // create a new image, waiting to be drawe

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
                return new int[] { originalImage.Width, originalImage.Height, width, height };
            }
            else
            {
                return new int[] { 0, 0, 0, 0 };
            }
        }

        public override bool OnStart()
        {
            // summary
            // 1.create database
            // 2.create image blob container
            // 3.create image queue
            // 4.set default connection maximum number

            // Set the maximum number of concurrent connections.
            ServicePointManager.DefaultConnectionLimit = 12; // connection with who ???

            // Read database connection string and open database. 
            //Database.SetInitializer<ImageScoreContext>(null);
            var dbConnString = CloudConfigurationManager.GetSetting("ImageScoreDbConnectionString");
            db = new ImageScoreContext(dbConnString); // get this.db

            // Open storage account using credentials from .cscfg file. // what is the relation between database and storage ??? former url latter data ???
            var storageAccount = CloudStorageAccount.Parse // this is an account
                (RoleEnvironment.GetConfigurationSettingValue("StorageConnectionString"));

            Trace.TraceInformation("Creating images queue");
            CloudQueueClient queueClient = storageAccount.CreateCloudQueueClient(); // using the storage account, we can create a queue client
            imagesQueue = queueClient.GetQueueReference("scoreimages"); // using the client, we can get a queue
            imagesQueue.CreateIfNotExists(); // if not exists, create the queue

            Trace.TraceInformation("Creating images blob container");
            var blobClient = storageAccount.CreateCloudBlobClient(); // using the account, we can get a blob client
            imagesBlobContainer = blobClient.GetContainerReference("scoreimages"); // then using the client, we finally get a blob container, this.container
            if (imagesBlobContainer.CreateIfNotExists()) // if not exists, create the container
            {
                // Enable public access on the newly created "images" container.
                imagesBlobContainer.SetPermissions(
                    new BlobContainerPermissions
                    {
                        PublicAccess = BlobContainerPublicAccessType.Blob
                    });
            }



            Trace.TraceInformation("Storage initialized");
            return base.OnStart();
        }

        public override void OnStop()
        {
            Trace.TraceInformation("ImageScoreWorker is stopping");

            base.OnStop();

            Trace.TraceInformation("ImageScoreWorker has stopped");
        }
    }
}
