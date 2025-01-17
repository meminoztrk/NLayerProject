﻿using NLayer.Core.DTOs.ProductDTOs;
using NLayer.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NLayer.Core.Repositories
{
    public interface IProductRepository : IGenericRepository<Product>
    {
        Task<List<Product>> GetProductWithCategory();
        Task<List<CategoryFeature>> GetCategoryFeaturesByCategoryId(int id);
        Task<List<Product>> GetUndeletedProductAsync();
        Task<Product> GetProductWithRelationsById(int id);
        Task<List<ProductIListDto>> GetProductWithFeaturesByCategoryId(int id);

    }
}
