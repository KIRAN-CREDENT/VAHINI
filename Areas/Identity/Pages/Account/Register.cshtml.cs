using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using Vahini.Data;
using Vahini.Models;
using Vahini.Services;

namespace Vahini.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class RegisterModel : PageModel
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<RegisterModel> _logger;
        private readonly ApplicationDbContext _db;

        public RegisterModel(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            ILogger<RegisterModel> logger,
            ApplicationDbContext db)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _logger = logger;
            _db = db;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public string? ReturnUrl { get; set; }

        public class InputModel
        {
            [Required]
            [EmailAddress]
            [Display(Name = "Email")]
            public string Email { get; set; } = string.Empty;

            [Required]
            [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
            [DataType(DataType.Password)]
            [Display(Name = "Password")]
            public string Password { get; set; } = string.Empty;

            [DataType(DataType.Password)]
            [Display(Name = "Confirm password")]
            [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
            public string ConfirmPassword { get; set; } = string.Empty;

            [Display(Name = "Register Type")]
            public string RegisterType { get; set; } = "Member";

            [Display(Name = "Organization Name")]
            public string? OrganizationName { get; set; }

            [Display(Name = "Access Code")]
            public string? AccessCode { get; set; }
        }

        public void OnGet(string? returnUrl = null)
        {
            ReturnUrl = returnUrl ?? Url.Content("~/");
        }

        public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");
            if (!ModelState.IsValid) return Page();
                var user = new ApplicationUser { UserName = Input.Email, Email = Input.Email };

                if (Input.RegisterType == "Admin")
                {
                    if (string.IsNullOrWhiteSpace(Input.OrganizationName))
                    {
                        ModelState.AddModelError(string.Empty, "Organization Name is required to create a new Workspace.");
                        return Page();
                    }

                    var accessCode = Guid.NewGuid().ToString("N").Substring(0, 6).ToUpper();
                    var org = new Organization
                    {
                        Name = Input.OrganizationName,
                        AccessCode = accessCode
                    };

                    _db.Organizations.Add(org);
                    await _db.SaveChangesAsync();

                    user.OrganizationId = org.Id;
                    user.IsApproved = true;
                    user.IsOrgAdmin = true;
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(Input.AccessCode))
                    {
                        ModelState.AddModelError(string.Empty, "Access Code is required to join an existing Workspace.");
                        return Page();
                    }

                    var code = Input.AccessCode.ToUpper();
                    var org = _db.Organizations.FirstOrDefault(o => o.AccessCode == code);
                    if (org == null)
                    {
                        ModelState.AddModelError(string.Empty, "Invalid Access Code.");
                        return Page();
                    }

                    user.OrganizationId = org.Id;
                    user.IsApproved = false;
                    user.IsOrgAdmin = false;
                }

                var result = await _userManager.CreateAsync(user, Input.Password);

                if (result.Succeeded)
                {
                    _logger.LogInformation("User {UserEmail} successfully registered as {Role}.", user.Email, Input.RegisterType);
                    var role = Input.RegisterType == "Admin" ? WorkflowService.RoleAdmin : WorkflowService.RoleMember;
                    await _userManager.AddToRoleAsync(user, role);

                    await _signInManager.SignInAsync(user, isPersistent: false);
                    return LocalRedirect(user.IsOrgAdmin ? "/Admin" : "/Member");
                }
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }

            return Page();
        }
    }
}
