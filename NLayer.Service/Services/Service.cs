﻿using Microsoft.EntityFrameworkCore;
using NLayer.Core.Repositories;
using NLayer.Core.Services;
using NLayer.Core.UnitOfWorks;
using NLayer.Service.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace NLayer.Service.Services
{
    public class Service<T> : IService<T> where T : class
    {
        private readonly IGenericRepository<T> _repository;
        private readonly IUnitOfWork _unitOfWork;

        public Service(IGenericRepository<T> repository, IUnitOfWork unitOfWork)
        {
            _repository = repository;
            _unitOfWork = unitOfWork;
        }

        public async Task<T> AddAsync(T entity)
        {
            await _repository.AddAsync(entity);
            await _unitOfWork.CommitAsync();
            return entity;
        }

        public async Task<IEnumerable<T>> AddRangeAsync(IEnumerable<T> entity)
        {
            await _repository.AddRangeAsync(entity);
            await _unitOfWork.CommitAsync();
            return entity;
        }

        public async Task<bool> AnyAsync(Expression<Func<T, bool>> expression)
        {
            return await _repository.AnyAsync(expression);  
        }

        public async Task<IEnumerable<T>> GetAllAsync()
        {
            return await _repository.GetAll().ToListAsync();
        }

        public async Task<T> GetByIdAsync(int id)
        {
            var hasProduct = await _repository.GetByIdAsync(id);
            if (hasProduct == null)
            {
                throw new NotFoundException($"{typeof(T).Name}({id}) not found");
            }
            return hasProduct;
        }

        public async Task RemoveAsync(T entity)
        {
            _repository.Remove(entity);
            await _unitOfWork.CommitAsync();
        }

        public async Task RemoveRangeAsync(IEnumerable<T> entity)
        {
            _repository.RemoveRange(entity);
            await _unitOfWork.CommitAsync();
        }

        public async Task UpdateAsync(T entity)
        {
            _repository.Update(entity);
            await _unitOfWork.CommitAsync();
        }    

        public IQueryable<T> Where(Expression<Func<T, bool>> expression)
        {
            return _repository.Where(expression);
        }

        public async Task UpdateIsActiveAsync(int id, bool IsActive)
        {
            var hasProduct = await _repository.GetByIdAsync(id);
            hasProduct.GetType().GetProperty("IsActive").SetValue(hasProduct, IsActive, null);
            _repository.Update(hasProduct);
            await _unitOfWork.CommitAsync();
        }
        public async Task UpdateIsDeletedAsync(int id, bool IsDeleted)
        {
            var hasProduct = await _repository.GetByIdAsync(id);
            hasProduct.GetType().GetProperty("IsDeleted").SetValue(hasProduct, IsDeleted, null);
            _repository.Update(hasProduct);
            await _unitOfWork.CommitAsync();
        }
    }
}
