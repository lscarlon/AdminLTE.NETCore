﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using AdminPanel.Attributes;
using AdminPanel.Identity;
using AdminPanel.Models;
using AdminPanel.Common;
using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace AdminPanel.Controllers
{

    [DisplayOrder(-1)]
    public class LoginController : CustomController
    {
        private readonly SignInManager<User> signInManager;
        private readonly UserManager<User> userManager;

        public LoginController(SignInManager<User> signInManager, UserManager<User> userManager)
        {
            this.signInManager = signInManager;
            this.userManager = userManager;
        }

        [AllowAnonymous]
        [HttpGet]
        public IActionResult Login(string returnUrl = null, string UserName = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            ViewData["UserName"] = UserName;
            if (TempData["ConfirmEmailMessage"] != null && Convert.ToBoolean(TempData["ConfirmEmailMessage"]))
            {
                ViewData["ConfirmEmailMessage"] = "You need to confirm your email to login";
                TempData.Remove("ConfirmEmailMessage");
            }
            return View();
        }

        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(IdentityLoginViewModel model, string returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            if (ModelState.IsValid)
            {
                var result = await signInManager.PasswordSignInAsync(model.UserName, model.Password, model.RememberMe, lockoutOnFailure: false);
                if (result.Succeeded)
                {
                    return RedirectToLocal(returnUrl);
                }
                else
                {
                    ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                    return View(model);
                }
            }
            return View(model);
        }

        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ExternalLogin(string provider, string returnUrl = null)
        {
            // Request a redirect to the external login provider.
            var redirectUrl = Url.Action("ExternalLoginCallback", "Login", new { ReturnUrl = returnUrl });
            var properties = signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
            return new ChallengeResult(provider, properties);
        }

        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> ExternalLoginCallback(string returnUrl = null, string remoteError = null)
        {
            if (remoteError != null)
            {
                ModelState.AddModelError(string.Empty, $"Error from external provider: {remoteError}");
                return View(nameof(Login));
            }

            var info = await signInManager.GetExternalLoginInfoAsync();
            if (info == null)
                return RedirectToAction(nameof(Login));

            // Sign in the user with this external login provider if the user already has a login.
            var result = await signInManager.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, false);
            if (result.Succeeded)
            {
                //_logger.LogInformation(5, "User logged in with {Name} provider.", info.LoginProvider);
                return RedirectToLocal(returnUrl);
            }
            if (result.IsLockedOut)
                return View("Lockout");

            // If the user does not have an account, then ask the user to create an account.
            ViewData["ReturnUrl"] = returnUrl;
            ViewData["LoginProvider"] = info.LoginProvider;
            var email = info.Principal.FindFirstValue(ClaimTypes.Email);
            var firstName = info.Principal.FindFirstValue(ClaimTypes.GivenName);
            var lastName = info.Principal.FindFirstValue(ClaimTypes.Surname);
            return View("ExternalLoginConfirmation", new ExternalLoginConfirmationViewModel
            {
                Email = email,
                UserName = email.Split('@')[0],
                Name = firstName + " " + lastName
            });
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExternalLoginConfirmation(ExternalLoginConfirmationViewModel model, string returnUrl = null)
        {
            if (signInManager.IsSignedIn(User))
            {
                return RedirectToLocal(returnUrl);
            }

            if (ModelState.IsValid)
            {
                // Get the information about the user from the external login provider
                var info = await signInManager.GetExternalLoginInfoAsync();
                if (info == null)
                {
                    return RedirectToAction(nameof(Login));
                    //return View("ExternalLoginFailure");
                }
                var user = new User
                {
                    Name = model.Name,
                    UserName = model.UserName,
                    Email = model.Email
                };
                var result = await userManager.CreateAsync(user);
                if (result.Succeeded)
                {
                    // Email confermata di default per i login esterni
                    user.EmailConfirmed = true;
                    result = await userManager.AddLoginAsync(user, info);
                    if (result.Succeeded)
                    {
                        await signInManager.SignInAsync(user, isPersistent: false);
                        return RedirectToLocal(returnUrl);
                    }
                }

            }

            ViewBag.ReturnUrl = returnUrl;
            return View(model);
        }

        private IActionResult RedirectToLocal(string returnUrl)
        {
            if (Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            else
            {
                return RedirectToAction(nameof(HomeController.Default), "Home");
            }
        }

        [AllowAnonymous]
        public IActionResult AccessDenied()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ScriptAfterPartialView("")]
        public async Task<IActionResult> SignOff()
        {
            await signInManager.SignOutAsync();
            return RedirectToAction("Login");
        }

        public IActionResult LockScreen()
        {
            return View();
        }

        [AllowAnonymous]
        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register([FromServices] IEmailService smtpClient, IdentityRegisterViewModel model)
        {
            if (ModelState.IsValid)
            {
                if (model.Password == model.RetypePassword && model.TermsAccepted)
                {
                    var user = new User()
                    {
                        Name = model.Name,
                        UserName = model.UserName,
                        Email = model.Email
                    };
                    IdentityResult result = await userManager.CreateAsync(user, model.Password);
                    if (result.Succeeded)
                    {
                        var code = await userManager.GenerateEmailConfirmationTokenAsync(user);
                        var callBackUrl = Url.Action(
                            "ConfirmEmail", "Login",
                            values: new { userId = user.Id, code },
                            protocol: Request.Scheme);

                        EmailMessage emailMessage = new EmailMessage
                        {
                            FromAddress = new EmailAddress { Name = "AdminPanel", Address = "adminpanel@l.carlone.it" },
                            Subject = "Confirm your email",
                            Content = $"Please confirm your account by <a href='{HtmlEncoder.Default.Encode(callBackUrl)}'>clicking here</a>."
                        };
                        emailMessage.ToAddresses.Add(new EmailAddress { Name = user.Name, Address = user.Email });
                        smtpClient.Send(emailMessage, out string response);

                        //Auto login
                        //await signInManager.SignInAsync(user, isPersistent: false);
                        //return RedirectToLocal(null);
                        TempData["ConfirmEmailMessage"] = true;
                        return RedirectToAction("Login");
                    }
                    else
                    {
                        foreach (string error in result.Errors.Select(e => e.Description).ToArray())
                        {
                            ModelState.AddModelError(string.Empty, error);
                        }
                        return View(model);
                    }
                }
                else
                {
                    ModelState.AddModelError(string.Empty, "Password mismatch or Terms not accepted");
                    return View(model);
                }
            }

            return View(model);
        }

        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> ConfirmEmail(string userId, string code)
        {
            if (userId != null && code != null)
            {
                var user = await userManager.FindByIdAsync(userId);
                IdentityResult result = await userManager.ConfirmEmailAsync(user, code);
                if (result.Succeeded)
                {
                    ViewBag.UserName = user.UserName;
                    return View("ConfirmEmail");
                }
            }
            return RedirectToAction("Login");
        }

        [AllowAnonymous]
        [HttpGet]
        public IActionResult RecoverPassword(string userId, string code)
        {
            if (userId == null || code == null)
            {
                return View();
            }
            else
            {
                ViewData["userId"] = userId;
                ViewData["code"] = code;
                User user = userManager.FindByIdAsync(userId).Result;
                if (user != null)
                {
                    ViewData["UserName"] = user.UserName;
                    ViewData["Email"] = user.Email;
                }
                return View();
            }
        }

        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult RecoverPassword([FromServices] IEmailService smtpClient, IdentityRecoverPasswordViewModel model)
        {
            if (ModelState.IsValid)
            {
                if (model.Token == null && model.UserName == null) //prima richiesta: invio mail
                {
                    User user = userManager.FindByEmailAsync(model.Email).Result;
                    if (user == null || !(userManager.IsEmailConfirmedAsync(user).Result))
                    {
                        ModelState.AddModelError(string.Empty, "Email non associata a nessun utente attivo");
                        return View(model);
                    }
                    else
                    {
                        var code = userManager.GeneratePasswordResetTokenAsync(user).Result;
                        var callBackUrl = Url.Action(
                            "RecoverPassword", "Login",
                            values: new { userId = user.Id, code },
                            protocol: Request.Scheme);

                        EmailMessage emailMessage = new EmailMessage
                        {
                            FromAddress = new EmailAddress { Name = "AdminPanel", Address = "adminpanel@l.carlone.it" },
                            Subject = "Reset Password Link",
                            Content = $"Your username is '{user.UserName}'. To reset your password please follow this link <a href='{HtmlEncoder.Default.Encode(callBackUrl)}'>clicking here</a>."
                        };
                        emailMessage.ToAddresses.Add(new EmailAddress { Name = user.Name, Address = user.Email });
                        smtpClient.Send(emailMessage, out string response);
                        //TempData["ConfirmEmailMessage"] = true;
                        return RedirectToAction("Login");
                    }
                }
                else //post con la nuova password
                {
                    if (model.Password == model.RetypePassword)
                    {
                        User user = userManager.FindByEmailAsync(model.Email).Result;
                        if (user.UserName != model.UserName)
                        {
                            ModelState.AddModelError(string.Empty, "Email and UserName doesn't match");
                            return View(model);
                        }
                        IdentityResult result = userManager.ResetPasswordAsync(user, model.Token, model.Password).Result;
                        if (result.Succeeded)
                        {
                            TempData["ConfirmEmailMessage"] = true;
                            return RedirectToAction("Login");
                        }
                        else
                        {
                            ModelState.AddModelError(string.Empty, "Error resetting password");
                            return View(model);
                        }
                    }
                    else
                    {
                        ModelState.AddModelError(string.Empty, "Password mismatch");
                        ViewData["UserName"] = model.UserName;
                        ViewData["Email"] = model.Email;
                        return View(model);
                    }
                }

            }
            return View(model);
        }
    }
}