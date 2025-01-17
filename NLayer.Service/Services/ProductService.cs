﻿using AutoMapper;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using NLayer.Core;
using NLayer.Core.DTOs;
using NLayer.Core.DTOs.BrandDTOs;
using NLayer.Core.DTOs.CartDTOs;
using NLayer.Core.DTOs.FeatureDTOs;
using NLayer.Core.DTOs.ProductDTOs;
using NLayer.Core.Models;
using NLayer.Core.Repositories;
using NLayer.Core.Services;
using NLayer.Core.UnitOfWorks;
using NLayer.Service.Helper;

namespace NLayer.Service.Services
{
    public class ProductService : Service<Product>, IProductService
    {
        private readonly IProductRepository _productRepository;
        private readonly ICategoryRepository _categoryRepository;
        private readonly IFeatureDetailRepository _featureDetailRepository;
        private readonly IProductFeatureService _productFeatureService;
        private readonly IFeatureDetailService _featureDetailService;
        private readonly IFeatureService _featureService;
        private readonly IProductImageService _productImageService;
        private readonly ICartService _cartService;
        private readonly ICartRepository _cartRepository;
        private readonly IMapper _mapper;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public ProductService(IGenericRepository<Product> repository, ICategoryRepository categoryRepository, ICartRepository cartRepository, IUnitOfWork unitOfWork,
                                                 ICartService cartService, IFeatureService featureService, IProductRepository productRepository, IProductFeatureService productFeatureService, 
                                                 IFeatureDetailService featureDetailService, IProductImageService productImageService, IFeatureDetailRepository featureDetailRepository,
                                                 IWebHostEnvironment webHostEnvironment, IMapper mapper, IHttpContextAccessor httpContextAccessor) : base(repository, unitOfWork)
        {
            _productRepository = productRepository;
            _featureDetailRepository = featureDetailRepository;
            _productFeatureService = productFeatureService;
            _featureDetailService = featureDetailService;
            _featureService = featureService;
            _categoryRepository = categoryRepository;
            _productImageService = productImageService;
            _cartService = cartService;
            _cartRepository = cartRepository;
            _webHostEnvironment = webHostEnvironment;
            _httpContextAccessor = httpContextAccessor;
            _mapper = mapper;
        }

        protected List<ProductCatChildDto> CategoryLoop(Category main)
        {
            List<ProductCatChildDto> categorySub = new List<ProductCatChildDto>();
            var subs = _categoryRepository.Where(x => x.SubId == main.Id && x.Id != x.SubId && x.IsDeleted == false).ToList();
            foreach (var sub in subs)
            {
                categorySub.Add(new ProductCatChildDto { Value = sub.Id, Label = sub.Name, Children = subs.Count != 0 ? CategoryLoop(sub) : null });
            }
            return categorySub;
        }

        protected async Task<List<ProductIListDto>> CategorySubLoop(int id)
        {
            List<ProductIListDto> products = new List<ProductIListDto>();
            var subCategories = _categoryRepository.Where(x => x.SubId == id && x.IsActive == true && x.IsDeleted == false).ToList();
            foreach (var category in subCategories)
            {
                if (await _categoryRepository.AnyAsync(x => x.SubId == category.Id && x.IsActive == true && x.IsDeleted == false))
                {
                    //sonsuz fonksiyon
                    products.AddRange(await CategorySubLoop(category.Id));
                }
                else
                {
                    //ekle
                    products.AddRange(await _productRepository.GetProductWithFeaturesByCategoryId(category.Id));
                }
            }
            return products;
        }

        public async Task<CustomResponseDto<List<ProductCatChildDto>>> GetCategoryWithChild()
        {
            var mainCategories = await _categoryRepository.GetAllMainCategoryAsync();

            List <ProductCatChildDto> category = new List<ProductCatChildDto>();
            foreach (var main in mainCategories)
            {
                category.Add(new ProductCatChildDto { Value = main.Id, Label = main.Name, Children = CategoryLoop(main) });
            }
            return CustomResponseDto<List<ProductCatChildDto>>.Success(200, category);
        }
       
        public async Task<CustomResponseDto<List<ProductWithCategoryDto>>> GetProductWithCategory()
        {
            var product = await _productRepository.GetProductWithCategory();

            var productDto =  _mapper.Map<List<ProductWithCategoryDto>>(product);

            return CustomResponseDto<List<ProductWithCategoryDto>>.Success(200, productDto);
        }

        public async Task<CustomResponseDto<List<CategoryFeatureWithNameDto>>> GetCategoryFeaturesByCategoryId(int id)
        {
            var features = await _productRepository.GetCategoryFeaturesByCategoryId(id);

            var featureDto = _mapper.Map<List<CategoryFeatureWithNameDto>>(features);

            return CustomResponseDto<List<CategoryFeatureWithNameDto>>.Success(200, featureDto);
        }

        public async Task<CustomResponseDto<NoContentDto>> SaveProduct(ProductPostDto product)
        {
            Product sproduct = new Product();
            sproduct.CategoryId = product.CategoryId;
            sproduct.BrandId = product.BrandId;
            sproduct.Name = product.Name;
            sproduct.Description = product.Explain;
            sproduct.CreatedDate = DateTime.Now;
            sproduct.IsActive = product.IsActive;
            foreach (var item in product.ProductFeatures)
            {
                sproduct.Stock += item.Stock;
            }

            await AddAsync(sproduct);

            List<ProductFeature> productFeatures = new List<ProductFeature>();
            foreach (var item in product.ProductFeatures)
            {
                productFeatures.Add(new ProductFeature()
                {
                    ProductId = sproduct.Id,
                    Color = item.Color,
                    Stock = item.Stock,
                    FePrice = item.FePrice,
                    Status = item.Status,
                });
            }

            
            await _productFeatureService.AddRangeAsync(productFeatures);

            
            List<FeatureDetail> featureDetails = new List<FeatureDetail>();
            if(product.CategoryFeatures != null)
            {
                foreach (var item in product.CategoryFeatures)
                {
                    featureDetails.Add(new FeatureDetail()
                    {
                        CategoryFeatureId = item.CategoryFeatureId,
                        ProductId = sproduct.Id,
                        Value = item.Value,
                        IsActive = true
                    });
                }
                await _featureDetailService.AddRangeAsync(featureDetails);
            }
            

            List<ProductImage> productImages = new List<ProductImage>();
            if(product.Pictures != null)
            {
                foreach (var item in product.Pictures)
                {
                    string wwwRootPath = _webHostEnvironment.WebRootPath;
                    string fileName = Path.GetFileNameWithoutExtension(item.FileName);
                    string extensions = Path.GetExtension(item.FileName);
                    string now = DateTime.Now.ToString("yymmssfff");
                    string path = Path.Combine(wwwRootPath + "/img/product/", fileName + now + extensions);
                    using (var fileStream = new FileStream(path, FileMode.Create))
                    {
                        await item.CopyToAsync(fileStream);
                    }

                    productImages.Add(new ProductImage()
                    {
                        ProductId = sproduct.Id,
                        Path = fileName + now + extensions,
                        IsActive = true
                    });
                }

                await _productImageService.AddRangeAsync(productImages);
            }
            

            return CustomResponseDto<NoContentDto>.Success(200, "Ürün Eklendi");
        }
       
        public async Task<CustomResponseDto<NoContentDto>> EditProduct(int id, ProductPostDto product)
        {
            var editProduct = await _productRepository.GetByIdAsync(id);
            if(editProduct != null)
            {
                editProduct.BrandId = product.BrandId;
                editProduct.CategoryId = product.CategoryId;
                editProduct.Name = product.Name;
                editProduct.Description = product.Explain;
                editProduct.IsActive = product.IsActive;
                await UpdateAsync(editProduct);

                #region Product Features
                var productFeatures = _productFeatureService.Where(x => x.ProductId == id).ToList();
                List<ProductFeature> listFeatures = new List<ProductFeature>();
                foreach (var feature in product.ProductFeatures)
                {
                    listFeatures.Add(new ProductFeature()
                    {
                        ProductId = editProduct.Id,
                        Color = feature.Color,
                        Stock = feature.Stock,
                        FePrice = feature.FePrice,
                        Status = feature.Status,
                    });
                }
                await _productFeatureService.AddRangeAsync(listFeatures);
                await _productFeatureService.RemoveRangeAsync(productFeatures);
                #endregion

                #region Category Features
                var categoryFeatures = _featureDetailService.Where(x => x.ProductId == id).ToList();
                List<FeatureDetail> featureDetails = new List<FeatureDetail>();
                if(product.CategoryFeatures != null)
                {
                    foreach (var feature in product.CategoryFeatures)
                    {
                        featureDetails.Add(new FeatureDetail()
                        {
                            ProductId = editProduct.Id,
                            CategoryFeatureId = feature.CategoryFeatureId,
                            Value = feature.Value,
                            IsActive = true
                        });
                    }
                    await _featureDetailService.AddRangeAsync(featureDetails);
                    await _featureDetailService.RemoveRangeAsync(categoryFeatures);
                }
                
                #endregion

                #region Product Images
                var productImages = _productImageService.Where(x => x.ProductId == id).ToList();
                foreach (var image in productImages)
                {
                    var path = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot\\img\\product", image.Path);
                    if (System.IO.File.Exists(path))
                    {
                        System.IO.File.Delete(path);
                    }
                }
                await _productImageService.RemoveRangeAsync(productImages);

                if(product.Pictures != null)
                {
                    List<ProductImage> protImages = new List<ProductImage>();
                    foreach (var item in product.Pictures)
                    {
                        string wwwRootPath = _webHostEnvironment.WebRootPath;
                        string fileName = Path.GetFileNameWithoutExtension(item.FileName);
                        string extensions = Path.GetExtension(item.FileName);
                        string now = DateTime.Now.ToString("yymmssfff");
                        string path = Path.Combine(wwwRootPath + "/img/product/", fileName.Substring(0, 1) + now + extensions);
                        using (var fileStream = new FileStream(path, FileMode.Create))
                        {
                            await item.CopyToAsync(fileStream);
                        }

                        protImages.Add(new ProductImage()
                        {
                            ProductId = editProduct.Id,
                            Path = fileName.Substring(0, 1) + now + extensions,
                            IsActive = true
                        });
                    }
                    await _productImageService.AddRangeAsync(protImages);
                }
                #endregion


                return CustomResponseDto<NoContentDto>.Success(200, "Ürün Güncellendi");
            }
            else
            {
                return CustomResponseDto<NoContentDto>.Fail(404, "Ürün id bulunamadı");
            }
            
        }
       
        public async Task<CustomResponseDto<List<ProductListDto>>> GetUndeletedProductAsync()
        {
            var products = await _productRepository.GetUndeletedProductAsync();

            var productDto = _mapper.Map<List<ProductListDto>>(products);

            return CustomResponseDto<List<ProductListDto>>.Success(200, productDto);
        }

        public async Task<CustomResponseDto<ProductForEditDto>> GetProduct(int id)
        {
            var product = await _productRepository.GetByIdAsync(id);
            if (product != null)
            {
                ProductForEditDto productForEdit = new ProductForEditDto();
                productForEdit.Id = product.Id;
                productForEdit.BrandId = (int)product.BrandId;
                productForEdit.Name = product.Name;
                productForEdit.Description = product.Description;
                productForEdit.IsActive = product.IsActive;

                #region Category Navigation Section
                List<int> cat = new List<int>();
                while (true)
                {
                    var Category = _categoryRepository.Where(x => x.Id == product.CategoryId).FirstOrDefault();
                    product.CategoryId = Category.SubId;
                    cat.Add(Category.Id);
                    cat.Sort();
                    if (Category.Id == Category.SubId)
                    {
                        break;
                    }
                }
                productForEdit.CategoryId = cat;
                #endregion

                #region Product Feature Section
                List<ProductFeatureDto> features = new List<ProductFeatureDto>();
                var getFeatures = _productFeatureService.Where(x=>x.ProductId == id).ToList();
                foreach (var feature in getFeatures)
                {
                    features.Add(new ProductFeatureDto { Color = feature.Color, Stock = feature.Stock, FePrice = feature.FePrice, Status = feature.Status });
                }
                productForEdit.ProductFeatures = features;
                #endregion

                #region Category Feature Section
                List<ProductFeatureDetailWithNameDto> featureDetails = new List<ProductFeatureDetailWithNameDto>();
                var getDetails = await _featureDetailRepository.GetDetailWithFeatureNameByProductId(id);
                foreach (var detail in getDetails)
                {
                    featureDetails.Add(new ProductFeatureDetailWithNameDto { CategoryFeatureId = (int)detail.CategoryFeatureId, Value = detail.Value, Name = detail.CategoryFeature.Name  });
                }
                productForEdit.CategoryFeaturesDetails = featureDetails;
                #endregion

                #region Image Section
                var images = await _productImageService.GetAllAsync();
                List<ProductImageDto> productImages = new List<ProductImageDto>();

                var request = _httpContextAccessor.HttpContext.Request;
                foreach (var image in images.Where(x=>x.ProductId == id).ToList())
                {
                    string path = request.Scheme + "://" + request.Host.Value + "/img/product/" + image.Path;
                    productImages.Add(new ProductImageDto { Uid = image.Id, Name = image.Path, Url = path });
                }
                productForEdit.Images = productImages;
                #endregion

                return CustomResponseDto<ProductForEditDto>.Success(200, productForEdit);
            }
            else
            {
                return CustomResponseDto<ProductForEditDto>.Fail(404, "Id not found");
            }
        }

        public List<ProductCatChildWithTitleDto> GetCategoryWithTitleChild(int index, Category main, List<string> categories,List<Category> cats, string path)
        {
            List<ProductCatChildWithTitleDto> category = new List<ProductCatChildWithTitleDto>();
            if (index < categories.Count())
            {
                var maincat = cats.Where(x => SeoHelper.ToSeoUrl(x.Name) == categories[index]).FirstOrDefault();
                string lastPath = path + "/" + categories[index];
                category.Add(new ProductCatChildWithTitleDto { Key = lastPath, Title = maincat.Name, Children = GetCategoryWithTitleChild(index + 1, maincat, categories, cats, lastPath) });
            }
            else
            {
                var endCategories = cats.Where(x => x.Id != x.SubId && x.SubId == main.Id).Select(x=>new ProductCatChildWithTitleDto
                {
                    Key = path + "/" + SeoHelper.ToSeoUrl(x.Name),
                    Title = x.Name,
                    Children = null
                }).ToList();

                category.AddRange(endCategories);
            }
            
            return category;
        }

        public async Task<CustomResponseDto<ProductIDataDto>> GetProductsByCategoryName(List<string> categories)
        {
            ProductIDataDto productData = new ProductIDataDto();
            int lastCatId = 0;
            #region Navigation
           
            var cats = _categoryRepository.GetAll().Where(x=>x.IsDeleted == false && x.IsActive == true).ToList();
            List<ProductINavigationDto> cat = new List<ProductINavigationDto>();
            int i = 0;
            string mainpath = "/kategori";
            foreach (var category in categories)
            {
                mainpath += "/" + category;
                Category catent = new Category();
                if (i == 0)
                {
                    catent = cats.Where(x => SeoHelper.ToSeoUrl(x.Name) == category).FirstOrDefault();
                    cat.Add(new ProductINavigationDto { Path = mainpath, Name = catent.Name});
                    lastCatId = catent.Id;
                }
                else
                {
                    catent = cats.Where(x => x.SubId == i && SeoHelper.ToSeoUrl(x.Name) == category).FirstOrDefault();
                    cat.Add(new ProductINavigationDto { Path = mainpath, Name = catent.Name });
                    lastCatId = catent.Id;
                }
                i = catent.Id;
            }    
            productData.Navigation = cat;
            #endregion

            #region Product
            List<ProductIListDto> products = new List<ProductIListDto>();
            if (await _categoryRepository.AnyAsync(x=> x.Id != x.SubId && x.SubId == i && x.IsActive == true && x.IsDeleted == false))
            {  
                var subCategories = _categoryRepository.Where(x => x.Id != x.SubId && x.SubId == i && x.IsActive == true && x.IsDeleted == false).ToList();
                foreach (var category in subCategories)
                {
                    if(await _categoryRepository.AnyAsync(x => x.SubId == category.Id && x.IsActive == true && x.IsDeleted == false)){
                        //sonsuz fonksiyon
                        products.AddRange(await CategorySubLoop(category.Id));
                    } 
                    else
                    {
                        //ekle
                        products.AddRange(await _productRepository.GetProductWithFeaturesByCategoryId(category.Id));
                    }
                }
            }
            else
            {
                products = await _productRepository.GetProductWithFeaturesByCategoryId(i);
            }
            
            productData.Products = products;
            #endregion

            #region ProductNavigation
            ProductINavDto productINav = new ProductINavDto();
            productINav.CategoryName = productData.Navigation.Last().Name;
            productINav.ProductCount = productData.Products.Count();

            productData.ProductNav = productINav;
            #endregion

            #region Product Feature
            ProductIFeatureDto features = new ProductIFeatureDto();

            List<ProductCatChildWithTitleDto> treeData = new List<ProductCatChildWithTitleDto>();
            var maincat = cats.Where(x => SeoHelper.ToSeoUrl(x.Name) == categories[0]).FirstOrDefault();
            string path = "/kategori/" + categories[0];
            treeData.Add(new ProductCatChildWithTitleDto { Key = path, Title = maincat.Name, Children = GetCategoryWithTitleChild(1, maincat, categories, cats, path) });
            features.TreeData = treeData;

            


            var groupBrands = products.GroupBy(x => x.Brand).Select(x=> x.Key).ToList();
            features.Brands = groupBrands;

            var groupColors = products.GroupBy(x => x.Color).Select(x => x.Key).ToList();
            features.Colors = groupColors;

            if (!cats.Any(x => x.SubId == lastCatId))
            {
                //var groupFeatures = products.GroupBy(x => x.Features).Select(x => new CategoryFeatureWithValuesDto {  Name = x.Key }).ToList();
                var allfeatures = await _featureService.GetAllAsync();
                var groupFeatures = allfeatures.Where(x=>x.CategoryId == lastCatId).Select(x=> new CategoryFeatureWithValuesDto { 
                    Name = x.Name ,
                    Values = _featureDetailRepository.GetAll().Where(y=>y.CategoryFeatureId == x.Id).GroupBy(x=>x.Value).Select(x=>x.Key).ToList()
                    
                }).ToList();
                features.Values = groupFeatures;
            }

            productData.ProductFeatures = features;
            #endregion

            return CustomResponseDto<ProductIDataDto>.Success(200, productData);
        }

        public async Task<CustomResponseDto<ProductISingleDto>> GetSingleProduct(int id)
        {
            var product = await  _productRepository.GetProductWithRelationsById(id);
            ProductISingleDto singleDto = new ProductISingleDto();
            singleDto.Id = product.Id;
            singleDto.Name = product.Name;
            singleDto.Brand = product.Brand.Name;
            singleDto.Description = product.Description;
            #region Category Navigation Section
            List<ProductINavigationDto> cat = new List<ProductINavigationDto>();
            List<string> catNames = new List<string>();
            while (true)
            {
                var Category = _categoryRepository.Where(x => x.Id == product.CategoryId).FirstOrDefault();
                product.CategoryId = Category.SubId;
                catNames.Add(Category.Name);
                if (Category.Id == Category.SubId)
                {
                    break;
                }
            }

            string mainpath = "/kategori";
            for (int i = catNames.Count() - 1;i>= 0; i--)
            {
                mainpath += "/" + SeoHelper.ToSeoUrl(catNames[i]);
                cat.Add(new ProductINavigationDto { Name= catNames[i], Path= mainpath });
            }
            singleDto.Navigation = cat;
            #endregion

            #region Product Features
            var features = product.ProductFeatures.Select(x => new ProductFeatureWithIdDto { Id = x.Id, Color = x.Color, Status = x.Status, Stock = x.Stock, FePrice = x.FePrice }).ToList();
            singleDto.Features = features;
            #endregion

            #region Images
            List<string> images = new List<string>();
            var request = _httpContextAccessor.HttpContext.Request;
            images = product.ProductImages.Select(x => request.Scheme + "://" + request.Host.Value + "/img/product/" + x.Path).ToList();
            singleDto.Pictures = images;
            #endregion

            return CustomResponseDto<ProductISingleDto>.Success(200, singleDto);
        }

        public async Task<CustomResponseDto<NoContentDto>> AddManyCart(CartAddManyDto manyCart)
        {
            var carts = _cartService.Where(x => x.UserId == manyCart.UserId).ToList();
            List<Cart> newCart = new List<Cart>();
            foreach (var item in manyCart.Cart)
            {
                if (carts.Any(x => x.ProductFeatureId == item.Id))
                {
                    var selectedCart = carts.Where(x => x.ProductFeatureId == item.Id).FirstOrDefault();
                    selectedCart.Quantity += item.Count;
                    await _cartService.UpdateAsync(selectedCart);
                }
                else
                {
                    newCart.Add(new Cart { UserId = manyCart.UserId, ProductFeatureId = item.Id, Quantity = item.Count });    
                }
            }

            if(manyCart.Cart.Count() > 0) { await _cartService.AddRangeAsync(newCart); };
   

            return CustomResponseDto<NoContentDto>.Success(200, "Ürünler Sepete Eklendi");
        }
        public async Task<CustomResponseDto<NoContentDto>> AddCart(CartAddDto cart)
        {
            var carts = _cartService.Where(x => x.UserId == cart.UserId).ToList();
            if (carts.Any(x => x.ProductFeatureId == cart.Cart.Id))
            {
                var selectedCart = carts.Where(x => x.ProductFeatureId == cart.Cart.Id).FirstOrDefault();
                selectedCart.Quantity += cart.Cart.Count;
                await _cartService.UpdateAsync(selectedCart);
            }
            else
            {
                await _cartService.AddAsync(new Cart { UserId = cart.UserId, ProductFeatureId = cart.Cart.Id, Quantity = cart.Cart.Count });
            }
            return CustomResponseDto<NoContentDto>.Success(200, "Ürün Sepete Eklendi");
        }
        public async Task<CustomResponseDto<List<CartWithImageDto>>> GetCart(int id)
        {
            var cart = await _cartRepository.GetCartWithUserId(id);
            var request = _httpContextAccessor.HttpContext.Request;
            var selectedCart = cart.Select(x => new CartWithImageDto
            {
                Id = x.ProductFeatureId,
                Name = x.ProductFeature.Product.Name,
                Count = x.Quantity,
                Color = x.ProductFeature.Color,
                Price = x.ProductFeature.FePrice,
                Image = request.Scheme + "://" + request.Host.Value + "/img/product/" + x.ProductFeature.Product.ProductImages.FirstOrDefault().Path
            }).ToList();
            return CustomResponseDto<List<CartWithImageDto>>.Success(200, selectedCart);
        }

        public async Task<CustomResponseDto<NoContentDto>> DeleteCart(int userId, int productFeatureId)
        {
            var cart = _cartService.Where(x => x.UserId == userId && x.ProductFeatureId == productFeatureId).FirstOrDefault();
            await _cartService.RemoveAsync(cart);
            return CustomResponseDto<NoContentDto>.Success(200, "Ürün Sepetten Silindi");
        }

        public async Task<CustomResponseDto<NoContentDto>> SetCartQuantity(int userId, int productFeatureId, string method)
        {
            var cart = _cartService.Where(x => x.UserId == userId && x.ProductFeatureId == productFeatureId).FirstOrDefault();
            if(method == "increase")
            {
                cart.Quantity++;
            }
            else { 
                cart.Quantity--;
            }
            await _cartService.UpdateAsync(cart);
            return CustomResponseDto<NoContentDto>.Success(200, "Sepet Güncellendi");
        }
    }
}
