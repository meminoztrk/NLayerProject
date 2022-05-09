﻿using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using NLayer.Core.DTOs;
using NLayer.Core.Models;
using NLayer.Core.Services;
using NLayer.Service.Services;

namespace NLayer.API.Controllers
{
    public class UserController : CustomBaseController
    {
        private readonly IMapper _mapper;
        private readonly IUserService _userService;
        private readonly JwtService _jwtService;

        public UserController(IMapper mapper, IUserService userService, JwtService jwtService)
        {
            _mapper = mapper;
            _userService = userService;
            _jwtService = jwtService;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register(UserRegisterDto userRegisterDto)
        {
            userRegisterDto.Password = BCrypt.Net.BCrypt.HashPassword(userRegisterDto.Password);
            var user = await _userService.AddAsync(_mapper.Map<User>(userRegisterDto));

            return CreateActionResult(CustomResponseDto<User>.Success(201, user));
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login(UserLoginDto userLoginDto)
        {
            var user =  _userService.GetByUsername(userLoginDto.Username);

            if (user == null) return BadRequest(new { message = "Invalid Credentials" });

            if (!BCrypt.Net.BCrypt.Verify(userLoginDto.Password, user.Password))
            {
                return BadRequest(new { message = "Invalid Credentials" });
            }

            var jwt =  _jwtService.Generate(user.Id);

            Response.Cookies.Append("jwt", jwt, new CookieOptions
            {
                HttpOnly = true,
                Expires = DateTime.UtcNow.AddDays(7),
                IsEssential = true,
                SameSite = SameSiteMode.None,
                Secure = true
            });
            var userDto = _mapper.Map<UserDto>(user);
            return CreateActionResult(CustomResponseDto<UserDto>.Success(200, userDto));
        }

        [HttpGet("user")]
        public async Task<IActionResult> User()
        {
            try
            {
                var jwt = Request.Cookies["jwt"];

                var token = _jwtService.Verify(jwt);

                int id = int.Parse(token.Issuer);

                var getuser = await _userService.GetByIdAsync(id);
                var user = _mapper.Map<UserDto>(getuser);

                return Ok(user);
            }
            catch (Exception e)
            {
                return Unauthorized();
            }

        }

        [HttpPost("logout")]
        public IActionResult Logout()
        {

            Response.Cookies.Delete("jwt", new CookieOptions
            {
                HttpOnly = true,
                Expires = DateTime.UtcNow.AddDays(-1),
                IsEssential = true,
                SameSite = SameSiteMode.None,
                Secure = true
            });

            return Ok(new { message = "success" });
        }

    }
}