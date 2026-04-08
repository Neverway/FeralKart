using System;
using System.Collections.Generic;
using System.Net; 

// A single entry in the ban list file
public class BanEntry
{
    public string Token { get; set; } = "";
    public string Name { get; set; } = "";
    public string Reason { get; set; } = "";
}
