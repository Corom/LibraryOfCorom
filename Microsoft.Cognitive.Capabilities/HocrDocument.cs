using Microsoft.Azure.Search;
using Microsoft.Azure.Search.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Microsoft.Cognitive.Capabilities
{
    /// <summary>
    /// Indexed representation of a scanned document that uses the HOCR standard for encoding word position metadata of OCR documents 
    /// </summary>
    [SerializePropertyNamesAsCamelCase]
    public class HOCRDocument
    {
        StringWriter metadata = new StringWriter();
        StringWriter text = new StringWriter() { NewLine = " " };
        int pageCount = 0;

        public HOCRDocument(string name) : this()
        {
            this.Id = name;
        }

        public HOCRDocument()
        {
            metadata.WriteLine(@"<?xml version='1.0' encoding='UTF-8'?>
<!DOCTYPE html PUBLIC '-//W3C//DTD XHTML 1.0 Transitional//EN' 'http://www.w3.org/TR/xhtml1/DTD/xhtml1-transitional.dtd'>
<html xmlns='http://www.w3.org/1999/xhtml' xml:lang='en' lang='en'>
 <head>
  <title></title>
  <meta http-equiv='Content-Type' content='text/html;charset=utf-8' />
  <meta name='ocr-system' content='Microsoft Cognitive Services' />
  <meta name='ocr-capabilities' content='ocr_page ocr_carea ocr_par ocr_line ocrx_word'/>
 </head>
 <body>");
        }

        public IEnumerable<WordResult> AddPage(OcrHWResult hw, string imageUrl)
        {
            // page
            metadata.WriteLine($"  <div class='ocr_page' id='page_{pageCount}' title='image \"{imageUrl}\"; bbox 0 0 {hw.Width} {hw.Height}; ppageno {pageCount}'>");
            metadata.WriteLine($"    <div class='ocr_carea' id='block_{pageCount}_1'");

            var allwords = new List<WordResult>();

            int li = 0;
            int wi = 0;
            foreach (var line in hw.lines /*.SortLines(hw.lines)*/)
            {
                metadata.WriteLine($"    <span class='ocr_line' id='line_{pageCount}_{li}' title='baseline -0.002 -5; x_size 30; x_descenders 6; x_ascenders 6'>");

                var words = line.words.FirstOrDefault()?.boundingBox == null ? line.words : line.words.OrderBy(l => l.boundingBox[0]).ToArray();

                foreach (var word in words)
                {
                    var bbox = word.boundingBox != null && word.boundingBox.Length == 8 ? $"bbox {word.boundingBox[0]} {word.boundingBox[1]} {word.boundingBox[4]} {word.boundingBox[5]}" : "";
                    metadata.WriteLine($"      <span class='ocrx_word' id='word_{pageCount}_{li}_{wi}' title='{bbox}'>{word.text}</span>");
                    text.WriteLine(word.text);
                    wi++;
                    allwords.Add(word);
                }
                li++;
                metadata.WriteLine(" </span>"); // line

            }

            metadata.WriteLine("    </div>"); // reading area
            metadata.WriteLine("  </div>"); // page

            pageCount++;

            return allwords;
        }

        // Fields that are in the index
        private string id;

        [System.ComponentModel.DataAnnotations.Key]
        [IsFilterable]
        public string Id { get { return id; } set { id = value.Replace(".", "_"); } }

        [JsonIgnore]
        public int PageCount { get { return pageCount; } }

        [IsSearchable]
        public string Metadata
        {
            get { return metadata.ToString() + metadata.NewLine + "</body></html>"; }
        }

        [IsRetrievable(false)]
        [IsSearchable]
        public string Text
        {
            get { return text.ToString(); }
        }

        [IsFilterable]
        [IsFacetable]
        public string[] People { get; set; }

        [IsFilterable]
        [IsFacetable]
        public string[] Places { get; set; }

        [IsFilterable]
        [IsFacetable]
        public List<string> Tags { get; set; } = new List<string>();

        [IsFilterable]
        [IsFacetable]
        public double Adult { get; set; }

        [IsFilterable]
        [IsFacetable]
        public double Racy { get; set; }

    }
}
