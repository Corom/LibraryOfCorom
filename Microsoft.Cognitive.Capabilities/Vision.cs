using Microsoft.ProjectOxford.Vision;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Cognitive.Capabilities
{
    public class Vision
    {
        VisionServiceClient visionClient;

        public Vision(string visionApiKey)
        {
            visionClient = new VisionServiceClient(visionApiKey);
        }


        private static int[] ConvertBoundingBox(string bbText)
        {
            var bbox = bbText.Split(',').Select(b => int.Parse(b)).ToArray();
            //0-left
            //1-top
            //2-width
            //3-height
            return new int[] {
                bbox[0],         bbox[1],
                bbox[0]+bbox[2], bbox[1],
                bbox[0]+bbox[2], bbox[1] + bbox[3],
                bbox[0],         bbox[1] + bbox[3],
            };
        }

        public Task<OcrHWResult> GetText(string url)
        {
            return GetText(null, url);
        }

        public async Task<OcrHWResult> GetText(Stream stream, string url = null)
        {
            var visionResult = await (stream != null ? visionClient.RecognizeTextAsync(stream, "en", false) :  visionClient.RecognizeTextAsync(url, "en", false));

            var lines = visionResult.Regions.SelectMany(r => r.Lines).Select(l =>

                new lineResult()
                {
                    boundingBox = ConvertBoundingBox(l.BoundingBox),
                    words = l.Words.Select(w => new WordResult()
                    {
                        boundingBox = ConvertBoundingBox(w.BoundingBox),
                        text = w.Text
                    }).ToArray()
                }
            );

            
            var result = new OcrHWResult()
            {
                lines = lines.ToArray()
            };
            return result;
        }

        public Task<OcrHWResult> GetVision(string url)
        {
            return GetVision(null, url);
        }

        public async Task<OcrHWResult> GetVision(Stream stream, string url = null)
        {
            var features = new[] { VisualFeature.Tags, VisualFeature.ImageType, VisualFeature.Description, VisualFeature.Adult};
            var visionResult = stream != null ? 
                await visionClient.AnalyzeImageAsync(stream, features)
                : await visionClient.AnalyzeImageAsync(url, features);

            List<lineResult> lines = new List<lineResult>();
            lines.AddRange(visionResult.Description.Captions.Select(c => new lineResult()
            {
                words = c.Text.Split(' ').Select(w => new WordResult()
                {
                    text = w
                }).ToArray()
            }
            ));

            lines.Add(new lineResult()
            {
                words = visionResult.Tags.Select(t => new WordResult()
                {
                    text = t.Name
                }).ToArray()
            });

            var result = new OcrHWResult()
            {
                lines = lines.ToArray(),
                AdultScore = visionResult.Adult.AdultScore,
                RacyScore = visionResult.Adult.RacyScore,
                Tags = visionResult.Tags.Select(t => t.Name),
                Width = visionResult.Metadata.Width,
                Height = visionResult.Metadata.Height,
            };
            return result;
        }

        public Task<OcrHWResult> GetHandwritingText(string imageUrl)
        {
            return GetHandwritingText(null, imageUrl);
        }

        public async Task<OcrHWResult> GetHandwritingText(Stream stream, string url = null)
        {
            var client = new HttpClient();
            client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", "92e5723b229149519f717c1e1ae81443");

            var uri = "https://westus.api.cognitive.microsoft.com/vision/v1.0/recognizeText?handwriting=true";

            HttpResponseMessage response;

            // Request body
            if (stream != null)
            {
                using (var content = new StreamContent(stream))
                {
                    content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                    response = await client.PostAsync(uri, content);
                }
            }
            else
            {
                var json = JsonConvert.SerializeObject(new { url = url });
                using (var content = new StringContent(json))
                {
                    content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                    response = await client.PostAsync(uri, content);
                }
            }

            OcrHWResult result = null;
            IEnumerable<string> opLocation;

            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync();
                response.EnsureSuccessStatusCode();
            }



            if (response.Headers.TryGetValues("Operation-Location", out opLocation))
            {
                while (true)
                {
                    response = await client.GetAsync(opLocation.First());
                    var txt = await response.Content.ReadAsStringAsync();
                    var status = JsonConvert.DeserializeObject<AsyncStatusResult>(txt);
                    if (status.status == "Running" || status.status == "NotStarted")
                        await Task.Delay(TimeSpan.FromMilliseconds(500));
                    else
                    {
                        result = status.recognitionResult;
                        break;
                    }
                }
            }

            return result;
        }
    }


    public class AsyncStatusResult
    {
        public string status { get; set; }
        public OcrHWResult recognitionResult { get; set; }
    }

    public class OcrHWResult
    {
        public lineResult[] lines { get; set; }
        public IEnumerable<string> Tags { get; set; } = Enumerable.Empty<string>();
        public double RacyScore { get; set; }
        public double AdultScore { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }

        public IEnumerable<lineResult> SortLines(IEnumerable<lineResult> lines)
        {
            if (lines.First().boundingBox == null)
                return lines;

            var avgWordHeight = (float)lines.SelectMany(l => l.words).Average(w => w.Height);
            int lineTol = (int)(avgWordHeight * .75);
            //var lineGroups = lines.GroupBy(l => l.CenterY, new ClosenessComparer(lineTol));
            var lineGroups = GetClusters(lines, l => l.CenterY, lineTol);

            var foo = lineGroups.OrderBy(lg => lg.First().CenterY);

            //foreach (var lg in foo)
            //{
            //    Console.WriteLine("Y: " + lg.First().CenterY);
            //    foreach (var l in lg.OrderBy(l => l.StartX))
            //        Console.WriteLine("{0},{1} - {2}", l.StartX, l.CenterY, l.text);
            //    Console.WriteLine();
            //}

            return foo.SelectMany(lg => lg.OrderBy(l => l.StartX));
        }


        public static OcrHWResult FixLinesAndWordOrder(OcrHWResult result)
        {

            var allWords = result.lines.SelectMany(l => l.words);

            var avgWordHeight = (float)allWords.Average(w => w.Height);
            int lineTol = (int)(avgWordHeight * .4);
            var wordGroups = GetClusters(allWords, l => l.CenterY, lineTol);

            var foo = wordGroups.OrderBy(lg => lg.First().CenterY);

            Console.WriteLine();
            Console.WriteLine("text:");
            foreach (var lg in foo)
            {
                //Console.WriteLine("Y: " + lg.First().CenterY);
                foreach (var l in lg.OrderBy(l => l.StartX))
                    Console.Write(" " + l.text);
                Console.WriteLine();

            }

            return result;

        }


        private static IEnumerable<IEnumerable<T>> GetClusters<T>(IEnumerable<T> data, Func<T, double> getKey,
                                             double delta = 4.0)
        {
            var cluster = new List<T>();
            foreach (var item in data.OrderBy(getKey))
            {
                var key = getKey(item);
                if (cluster.Count > 0 && key > getKey(cluster[cluster.Count - 1]) + delta)
                {
                    yield return cluster;
                    cluster = new List<T>();
                }
                cluster.Add(item);
            }
            if (cluster.Count > 0)
                yield return cluster;
        }


        public OcrHWResult Concat(OcrHWResult result)
        {
            var newResult = new OcrHWResult();
            newResult.lines = lines.Concat(result.lines).ToArray();
            newResult.Tags = Tags.Concat(result.Tags).ToArray();
            newResult.Width = Width + result.Width;
            newResult.Height = Height + result.Height;
            return newResult;
        }

    }

    public class RegionResult
    {
        internal const int XUL = 0;
        internal const int YUL = 1;
        internal const int XUR = 2;
        internal const int YUR = 3;
        internal const int XLR = 4;
        internal const int YLR = 5;
        internal const int XLL = 6;
        internal const int YLL = 7;

        public int[] boundingBox { get; set; }
        public string text { get; set; }


        public IEnumerable<Point> GetPoints()
        {
            for (int i = 0; i < boundingBox.Length; i += 2)
            {
                yield return new Point(boundingBox[i], boundingBox[i + 1]);
            }
            yield return new Point(boundingBox[0], boundingBox[1]);
        }

        public int CenterY { get { return StartY + (Height / 2); } }
        public int StartX { get { return boundingBox[XUL]; } }
        public int StartY { get { return boundingBox[YUL]; } }
        public int Height { get { return boundingBox[YLL] - boundingBox[YUL]; } }
        public int Width { get { return boundingBox[XUR] - boundingBox[XUL]; } }
    }

    public class lineResult : RegionResult
    {
        public WordResult[] words { get; set; }
    }


    public class WordResult : RegionResult
    {
    }



}
