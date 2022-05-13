﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NLayer.Core.DTOs
{
    public class CategoryWithSubCount
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int SubCount { get; set; }
        public bool IsActive { get; set; }
    }
}
