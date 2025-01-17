﻿using NLayer.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NLayer.Core
{
    public class ProductFeature:BaseEntity
    {
        public string Color { get; set; }
        public int Stock { get; set; }
        public decimal FePrice { get; set; }
        public bool Status { get; set; }
        public int ProductId { get; set; }
        public Product Product { get; set; }
        public ICollection<Cart> Carts { get; set; }
    }
}
