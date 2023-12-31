using AngleSharp;
using AngleSharp.Dom;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using static System.Net.WebRequestMethods;
using Configuration = AngleSharp.Configuration;

namespace LET.GIF.Scrape
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var results = new List<Result>();

            var config = Configuration.Default.WithDefaultLoader();
            var context = BrowsingContext.New(config);

            Console.WriteLine("============================================================");
            Console.WriteLine();
            Console.WriteLine("                     LET.GIF.Scrape");
            Console.WriteLine("                       MMMMMM @LET");
            Console.WriteLine();
            Console.WriteLine("============================================================");

            Console.WriteLine();

            Console.WriteLine("paste url to scrape:");

            var url = Console.ReadLine();

            Console.WriteLine();

            Console.WriteLine("gif only (default no): yes/no?");

            var gifOnly = bool.Parse((Console.ReadLine()?.ToLower() == "yes" ? "true" : "false") ?? "false");

            Console.WriteLine();

            Console.WriteLine("unique list (default: yes): yes/no?");

            var unique = bool.Parse((Console.ReadLine()?.ToLower() == "yes" ? "true" : "false") ?? "false");

            Console.WriteLine();

            Console.WriteLine("starting...");

            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) " +
                "AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36 Edg/120.0.0.0");
            client.DefaultRequestHeaders.Add("Referer", "https://lowendtalk.com");

            var response = client.GetAsync(url).Result;
            if (response.IsSuccessStatusCode)
            {
                var body = response.Content.ReadAsStringAsync().Result;
                var doc = context.OpenAsync(req => req.Content(body)).Result;
                var totalPageStr = doc.QuerySelector(".LastPage")?.TextContent.Trim();

                Console.WriteLine("total page: " + totalPageStr);

                Console.Write("current page: page 1");
                var parseResults = GetResults(doc, client, gifOnly);
                if (unique)
                {
                    foreach (var parseResult in parseResults)
                    {
                        if (results.FirstOrDefault(v => v.Name == parseResult.Name) == null)
                        {
                            results.Add(parseResult);
                        }
                    }
                }
                else
                {
                    results.AddRange(parseResults);
                }

                if (totalPageStr != null)
                {
                    var totalPage = int.Parse(totalPageStr);
                    for (int i = 2; i <= totalPage; i++)
                    {
                        Console.Write("\rcurrent page: page " + i);

                        response = client.GetAsync(url + "/p" + i).Result;
                        if (response.IsSuccessStatusCode)
                        {
                            body = response.Content.ReadAsStringAsync().Result;

                            doc = context.OpenAsync(req => req.Content(body)).Result;

                            parseResults = GetResults(doc, client, gifOnly);
                            if (unique)
                            {
                                foreach (var parseResult in parseResults)
                                {
                                    if (results.FirstOrDefault(v => v.Name == parseResult.Name) == null)
                                    {
                                        results.Add(parseResult);
                                    }
                                }
                            } else
                            {
                                results.AddRange(parseResults);
                            }

                            Thread.Sleep(1000);
                        }
                    }

                }

                doc.Dispose();
                context.Dispose();

                Console.WriteLine();

                Console.WriteLine("total users with image: " + results.Count);

                CreateCSV(results, "output.csv");

                Console.WriteLine("output saved as output.csv");

                Console.WriteLine("finished");
                Console.ReadLine();
            }
        }

        static List<Result> GetResults(IDocument doc, HttpClient client, bool gifOnly = false)
        {
            var results = new List<Result>();

            var comments = doc.QuerySelectorAll(".DataBox-Comments > .MessageList > .Item");
            foreach (var comment in comments)
            {
                var result = new Result();

                result.Name = comment.QuerySelector(".Author")?.TextContent.Trim();
                result.CommentUrl = "https://lowendtalk.com" + comment.QuerySelector(".CommentMeta .Permalink")?.GetAttribute("href")?.Trim();

                comment.QuerySelector(".UserQuote")?.Remove();

                var imgs = comment.QuerySelectorAll(".Message img");

                if (!gifOnly)
                {
                    if (imgs.Count() > 0)
                    { 
                        result.ImgUrl = imgs.First()?.GetAttribute("src")?.Trim();

                        results.Add(result);
                    }
                }
                else
                {
                    foreach (var img in imgs)
                    {
                        client.DefaultRequestHeaders.Add("Accept", "image/*");

                        var imgUrl = img.GetAttribute("src")?.Trim();

                        if (imgUrl != null)
                        {

                            if (imgUrl.Contains(".webp"))
                            {
                                var imgResponse = client.GetAsync(imgUrl).Result;
                                if (imgResponse.IsSuccessStatusCode)
                                {
                                    using var imgStream = imgResponse.Content.ReadAsStreamAsync().Result;
                                    using var image = Image.Load<Rgba32>(imgStream);

                                    if (image.Frames.Count > 1)
                                    {
                                        result.ImgUrl = imgUrl;

                                        results.Add(result);

                                        break;

                                    }

                                }
                            }
                            else if (imgUrl.Contains(".gif"))
                            {
                                result.ImgUrl = imgUrl;



                                results.Add(result);

                                break;

                            } else
                            {
                                var ext = Path.GetExtension(imgUrl);

                                if (ext == null)
                                {
                                    var imgResponse = client.GetAsync(imgUrl, HttpCompletionOption.ResponseHeadersRead).Result;
                                    if (imgResponse.IsSuccessStatusCode)
                                    {
                                        if (imgResponse.Content.Headers.ContentType != null)
                                        {
                                            var contentType = imgResponse.Content.Headers.ContentType.MediaType;
                                            if (contentType == "image/gif")
                                            {
                                                result.ImgUrl = imgUrl;

                                                results.Add(result);

                                                break;
                                            }
                                        }
                                    }
                                }
                            }
                        }

                        client.DefaultRequestHeaders.Remove("Accept");
                    }
                }
            }

            return results;
        }
        public static void CreateCSV<T>(List<T> list, string filePath)
        {
            using (StreamWriter sw = new StreamWriter(filePath))
            {
                CreateHeader(list, sw);
                CreateRows(list, sw);
            }
        }
        private static void CreateHeader<T>(List<T> list, StreamWriter sw)
        {
            PropertyInfo[] properties = typeof(T).GetProperties();
            for (int i = 0; i < properties.Length - 1; i++)
            {
                sw.Write(properties[i].Name + ",");
            }
            var lastProp = properties[properties.Length - 1].Name;
            sw.Write(lastProp + sw.NewLine);
        }

        private static void CreateRows<T>(List<T> list, StreamWriter sw)
        {
            foreach (var item in list)
            {
                PropertyInfo[] properties = typeof(T).GetProperties();
                for (int i = 0; i < properties.Length - 1; i++)
                {
                    var prop = properties[i];
                    sw.Write(prop.GetValue(item) + ",");
                }
                var lastProp = properties[properties.Length - 1];
                sw.Write(lastProp.GetValue(item) + sw.NewLine);
            }
        }
    }

    internal class Result
    {
        public string? Name { get; set; }
        public string? ImgUrl { get; set; }

        public string? CommentUrl { get; set; }
    }
}
