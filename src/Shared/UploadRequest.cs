using System;
using System.Collections.Generic;

namespace BlazorApp.Shared
{
    public class UploadRequest
    {
        public string PersonName { get; set; } = string.Empty;
        public Dictionary<string, byte[]> Photos { get; set; } = new Dictionary<string, byte[]>();
    }
}
