﻿using NLayer.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NLayer.Core.Repositories
{
    public interface IBrandRepository:IGenericRepository<Brand>
    {
        Task<List<Brand>> GetUndeletedBrandAsync();
    }
}
