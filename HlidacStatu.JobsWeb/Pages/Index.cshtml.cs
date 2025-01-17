﻿using HlidacStatu.Ceny.Models;
using HlidacStatu.Ceny.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HlidacStatu.Ceny.Pages
{
    public class IndexModel : PageModel
    {
        
        public YearlyStatisticsGroup.Key? Key { get; set; }
        
        public void OnGet()
        {
            Key = HttpContext.TryFindKey();
        }
    }
}