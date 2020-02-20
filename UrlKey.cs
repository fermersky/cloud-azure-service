using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.WindowsAzure.Storage.Table;

namespace UrlShortener
{
    public class UrlKey : TableEntity
    {
        public int Id { get; set; }


    }
}
