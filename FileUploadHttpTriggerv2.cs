using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Net.Http;
using Microsoft.Azure.Storage.Blob;
using System.Collections.Generic;
using System.Linq;

public class FileUploadHttpTriggerv2
{
    [FunctionName("FileUploadHttpTriggerv2")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "files2")] HttpRequestMessage req,
        ILogger logger,
        [Blob("%AzureStorage:FilePath%", FileAccess.Write, Connection = "AzureStorage:ConnectionString")] CloudBlobContainer cloudBlobContainer)
    {
        logger.LogInformation($"{nameof(FileUploadHttpTriggerv2)} trigger function processed a request.");

        var multipartMemoryStreamProvider = new MultipartMemoryStreamProvider();

        await req.Content.ReadAsMultipartAsync(multipartMemoryStreamProvider);

        var file = multipartMemoryStreamProvider.Contents[0];
        
        var fileInfo = file.Headers.ContentDisposition;

        logger.LogInformation(JsonConvert.SerializeObject(req.Headers, Formatting.Indented));
        logger.LogInformation(JsonConvert.SerializeObject(file.Headers, Formatting.Indented));

        var blobName = $"{Guid.NewGuid()}{Path.GetExtension(fileInfo.FileName)}";
        blobName = blobName.Replace("\"", "");

        var cloudBlockBlob = cloudBlobContainer.GetBlockBlobReference(blobName);

        cloudBlockBlob.Properties.ContentType = file.Headers.ContentType.MediaType;
        cloudBlockBlob.Metadata.Add("origName", fileInfo.FileName);        
        var xff = req.Headers.FirstOrDefault( x => x.Key == "X-Forwarded-For" ).Value.FirstOrDefault();
        cloudBlockBlob.Metadata.Add("sourceIp", xff);
        
        using (var fileStream = await file.ReadAsStreamAsync())
        {
            await cloudBlockBlob.UploadFromStreamAsync(fileStream);
        }

        return (ActionResult) new OkObjectResult(new { name = blobName });
    }
}