﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NLayer.Core.Models
{
    public class ProductImage:BaseEntity
    {
        public string Path { get; set; }
        public int? ProductId { get; set; }
        public Product Product { get; set; }
        
    }
}
