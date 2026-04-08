using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlantShop.API.Data;
using PlantShop.API.DTOs;
using PlantShop.API.Models;
using System.Security.Claims;

namespace PlantShop.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class OrdersController : ControllerBase
    {
        private readonly AppDbContext _context;

        public OrdersController(AppDbContext context)
        {
            _context = context;
        }


        [HttpGet("cart")]
        [Authorize]
        public async Task<IActionResult> GetCart()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);

            if (userIdClaim == null)
                return Unauthorized();

            int userId = int.Parse(userIdClaim.Value);

            var cart = await _context.Orders
                .Include(o => o.Items)
                .ThenInclude(i => i.Product)
                .FirstOrDefaultAsync(o => o.UserId == userId && o.Status == "Cart");

            if (cart == null)
            {
                return Ok(new
                {
                    id = 0,
                    status = "Cart",
                    items = new List<object>()
                });
            }

            return Ok(new
            {
                id = cart.Id,
                status = cart.Status,
                items = cart.Items.Select(i => new
                {
                    id = i.Id,
                    quantity = i.Quantity,
                    price = i.Price,
                    product = new
                    {
                        id = i.Product.Id,
                        name = i.Product.Name
                    }
                })
            });
        }


        [HttpPost("cart/add")]
        [Authorize]
        public async Task<IActionResult> AddToCart(AddToCartDto dto)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);

            if (userIdClaim == null)
                return Unauthorized();

            int userId = int.Parse(userIdClaim.Value);

            var product = await _context.Products.FindAsync(dto.ProductId);

            if (product == null)
                return NotFound("Товар не знайдено");

            var cart = await _context.Orders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.UserId == userId && o.Status == "Cart");

            if (cart == null)
            {
                cart = new Order
                {
                    UserId = userId,
                    Status = "Cart"
                };

                _context.Orders.Add(cart);
                await _context.SaveChangesAsync();
            }

            var existingItem = cart.Items.FirstOrDefault(i => i.ProductId == dto.ProductId);

            if (existingItem != null)
            {
                existingItem.Quantity += dto.Quantity;
            }
            else
            {
                cart.Items.Add(new OrderItem
                {
                    ProductId = product.Id,
                    Quantity = dto.Quantity,
                    Price = product.Price
                });
            }

            await _context.SaveChangesAsync();

            return Ok("Товар додано в кошик");
        }

        
        [HttpPost("cart/pay")]
        [Authorize]
        public async Task<IActionResult> PayCart()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);

            if (userIdClaim == null)
                return Unauthorized();

            int userId = int.Parse(userIdClaim.Value);

            var cart = await _context.Orders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.UserId == userId && o.Status == "Cart");

            if (cart == null)
                return BadRequest("Кошик порожній");

            if (!cart.Items.Any())
                return BadRequest("У кошику немає товарів");

            cart.Status = "Paid";

            await _context.SaveChangesAsync();

            return Ok("Замовлення оплачено");
        }


        [HttpGet("my")]
        [Authorize]
        public async Task<IActionResult> GetMyOrders()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);

            if (userIdClaim == null)
                return Unauthorized();

            int userId = int.Parse(userIdClaim.Value);

            var orders = await _context.Orders
                .Include(o => o.Items)
                .ThenInclude(i => i.Product)
                .Where(o => o.UserId == userId && o.Status != "Cart")
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();

            var result = orders.Select(o => new
            {
                id = o.Id,
                status = o.Status,
                createdAt = o.CreatedAt,
                items = o.Items.Select(i => new
                {
                    id = i.Id,
                    quantity = i.Quantity,
                    price = i.Price,
                    product = new
                    {
                        id = i.Product.Id,
                        name = i.Product.Name
                    }
                })
            });

            return Ok(result);
        }
        [HttpDelete("cart/item/{itemId}")]
        [Authorize]
        public async Task<IActionResult> RemoveFromCart(int itemId)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);

            if (userIdClaim == null)
                return Unauthorized();

            int userId = int.Parse(userIdClaim.Value);

            var cart = await _context.Orders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.UserId == userId && o.Status == "Cart");

            if (cart == null)
                return NotFound("Кошик не знайдено");

            var item = cart.Items.FirstOrDefault(i => i.Id == itemId);

            if (item == null)
                return NotFound("Товар не знайдено");

            _context.OrderItems.Remove(item);
            await _context.SaveChangesAsync();

            return Ok("Товар видалено з кошика");
        }

        [HttpGet("all")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAllOrders()
        {
            var orders = await _context.Orders
                .Include(o => o.User)
                .Include(o => o.Items)
                .ThenInclude(i => i.Product)
                .Where(o => o.Status != "Cart")
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();

            var result = orders.Select(o => new
            {
                id = o.Id,
                status = o.Status,
                createdAt = o.CreatedAt,
                user = new
                {
                    userName = o.User.UserName,
                    email = o.User.Email
                },
                items = o.Items.Select(i => new
                {
                    id = i.Id,
                    quantity = i.Quantity,
                    price = i.Price,
                    product = new
                    {
                        id = i.Product.Id,
                        name = i.Product.Name
                    }
                })
            });

            return Ok(result);
        }


        [HttpPut("{id}/deliver")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> MarkAsDelivered(int id)
        {
            var order = await _context.Orders.FindAsync(id);

            if (order == null)
                return NotFound("Замовлення не знайдено");

            if (order.Status != "Paid")
                return BadRequest("Доставити можна тільки оплачене замовлення");

            order.Status = "Delivered";

            await _context.SaveChangesAsync();

            return Ok("Статус змінено");
        }
    }
}
