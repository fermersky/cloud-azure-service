using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.WindowsAzure.Storage.Table;

namespace UrlShortener
{
    public class UrlData : TableEntity
    {
        public string Url { get; set; }
        public int Count { get; set; }

    }
}
