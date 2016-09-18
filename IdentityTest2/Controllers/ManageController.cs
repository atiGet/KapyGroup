﻿using System;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.Owin;
using Microsoft.Owin.Security;
using IdentityTest2.Models;
using System.Text;
using System.Collections.Generic;
using System.IO;

//new
namespace IdentityTest2.Controllers
{
    [Authorize]
    public class ManageController : Controller
    {
        private kapymvc1Entities db = new kapymvc1Entities();
        private ApplicationSignInManager _signInManager;
        private ApplicationUserManager _userManager;

        public ManageController()
        {
        }

        public ManageController(ApplicationUserManager userManager, ApplicationSignInManager signInManager)
        {
            UserManager = userManager;
            SignInManager = signInManager;
        }

        public ApplicationSignInManager SignInManager
        {
            get
            {
                return _signInManager ?? HttpContext.GetOwinContext().Get<ApplicationSignInManager>();
            }
            private set
            {
                _signInManager = value;
            }
        }

        public ApplicationUserManager UserManager
        {
            get
            {
                return _userManager ?? HttpContext.GetOwinContext().GetUserManager<ApplicationUserManager>();
            }
            private set
            {
                _userManager = value;
            }
        }

        //
        // GET: /Manage/Index
        public async Task<ActionResult> Index(ManageMessageId? message)
        {
            ViewBag.StatusMessage =
                message == ManageMessageId.ChangePasswordSuccess ? "Your password has been changed."
                : message == ManageMessageId.SetPasswordSuccess ? "Your password has been set."
                : message == ManageMessageId.SetTwoFactorSuccess ? "Your two-factor authentication provider has been set."
                : message == ManageMessageId.Error ? "An error has occurred."
                : message == ManageMessageId.AddPhoneSuccess ? "Your phone number was added."
                : message == ManageMessageId.RemovePhoneSuccess ? "Your phone number was removed."
                : "";

            var userId = User.Identity.GetUserId<int>();
            var model = new IndexViewModel
            {
                HasPassword = HasPassword(),
                PhoneNumber = await UserManager.GetPhoneNumberAsync(userId),
                TwoFactor = await UserManager.GetTwoFactorEnabledAsync(userId),
                Logins = await UserManager.GetLoginsAsync(userId),
                BrowserRemembered = await AuthenticationManager.TwoFactorBrowserRememberedAsync(User.Identity.GetUserId())
            };
            return View(model);
        }

        //
        // POST: /Manage/RemoveLogin
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> RemoveLogin(string loginProvider, string providerKey)
        {
            ManageMessageId? message;
            var result = await UserManager.RemoveLoginAsync(User.Identity.GetUserId<int>(), new UserLoginInfo(loginProvider, providerKey));
            if (result.Succeeded)
            {
                var user = await UserManager.FindByIdAsync(User.Identity.GetUserId<int>());
                if (user != null)
                {
                    await SignInManager.SignInAsync(user, isPersistent: false, rememberBrowser: false);
                }
                message = ManageMessageId.RemoveLoginSuccess;
            }
            else
            {
                message = ManageMessageId.Error;
            }
            return RedirectToAction("ManageLogins", new { Message = message });
        }

        //
        // GET: /Manage/AddPhoneNumber
        public ActionResult AddPhoneNumber()
        {
            return View();
        }

        //
        // POST: /Manage/AddPhoneNumber
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> AddPhoneNumber(AddPhoneNumberViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }
            // Generate the token and send it
            var code = await UserManager.GenerateChangePhoneNumberTokenAsync(User.Identity.GetUserId<int>(), model.Number);
            if (UserManager.SmsService != null)
            {
                var message = new IdentityMessage
                {
                    Destination = model.Number,
                    Body = "Your security code is: " + code
                };
                await UserManager.SmsService.SendAsync(message);
            }
            return RedirectToAction("VerifyPhoneNumber", new { PhoneNumber = model.Number });
        }

        //
        // POST: /Manage/EnableTwoFactorAuthentication
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> EnableTwoFactorAuthentication()
        {
            await UserManager.SetTwoFactorEnabledAsync(User.Identity.GetUserId<int>(), true);
            var user = await UserManager.FindByIdAsync(User.Identity.GetUserId<int>());
            if (user != null)
            {
                await SignInManager.SignInAsync(user, isPersistent: false, rememberBrowser: false);
            }
            return RedirectToAction("Index", "Manage");
        }

        //
        // POST: /Manage/DisableTwoFactorAuthentication
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> DisableTwoFactorAuthentication()
        {
            await UserManager.SetTwoFactorEnabledAsync(User.Identity.GetUserId<int>(), false);
            var user = await UserManager.FindByIdAsync(User.Identity.GetUserId<int>());
            if (user != null)
            {
                await SignInManager.SignInAsync(user, isPersistent: false, rememberBrowser: false);
            }
            return RedirectToAction("Index", "Manage");
        }

        //
        // GET: /Manage/VerifyPhoneNumber
        public async Task<ActionResult> VerifyPhoneNumber(string phoneNumber)
        {
            var code = await UserManager.GenerateChangePhoneNumberTokenAsync(User.Identity.GetUserId<int>(), phoneNumber);
            // Send an SMS through the SMS provider to verify the phone number
            return phoneNumber == null ? View("Error") : View(new VerifyPhoneNumberViewModel { PhoneNumber = phoneNumber });
        }

        //
        // POST: /Manage/VerifyPhoneNumber
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> VerifyPhoneNumber(VerifyPhoneNumberViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }
            var result = await UserManager.ChangePhoneNumberAsync(User.Identity.GetUserId<int>(), model.PhoneNumber, model.Code);
            if (result.Succeeded)
            {
                var user = await UserManager.FindByIdAsync(User.Identity.GetUserId<int>());
                if (user != null)
                {
                    await SignInManager.SignInAsync(user, isPersistent: false, rememberBrowser: false);
                }
                return RedirectToAction("Index", new { Message = ManageMessageId.AddPhoneSuccess });
            }
            // If we got this far, something failed, redisplay form
            ModelState.AddModelError("", "Failed to verify phone");
            return View(model);
        }

        //
        // GET: /Manage/RemovePhoneNumber
        public async Task<ActionResult> RemovePhoneNumber()
        {
            var result = await UserManager.SetPhoneNumberAsync(User.Identity.GetUserId<int>(), null);
            if (!result.Succeeded)
            {
                return RedirectToAction("Index", new { Message = ManageMessageId.Error });
            }
            var user = await UserManager.FindByIdAsync(User.Identity.GetUserId<int>());
            if (user != null)
            {
                await SignInManager.SignInAsync(user, isPersistent: false, rememberBrowser: false);
            }
            return RedirectToAction("Index", new { Message = ManageMessageId.RemovePhoneSuccess });
        }

        //
        // GET: /Manage/ChangePassword
        public ActionResult ChangePassword()
        {
            return View();
        }

        //
        // POST: /Manage/ChangePassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }
            var result = await UserManager.ChangePasswordAsync(User.Identity.GetUserId<int>(), model.OldPassword, model.NewPassword);
            if (result.Succeeded)
            {
                var user = await UserManager.FindByIdAsync(User.Identity.GetUserId<int>());
                if (user != null)
                {
                    await SignInManager.SignInAsync(user, isPersistent: false, rememberBrowser: false);
                }
                return RedirectToAction("Index", new { Message = ManageMessageId.ChangePasswordSuccess });
            }
            AddErrors(result);
            return View(model);
        }



        //
        // GET: /Manage/SetPassword
        public ActionResult SetPassword()
        {
            return View();
        }

        //
        // POST: /Manage/SetPassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> SetPassword(SetPasswordViewModel model)
        {
            if (ModelState.IsValid)
            {
                var result = await UserManager.AddPasswordAsync(User.Identity.GetUserId<int>(), model.NewPassword);
                if (result.Succeeded)
                {
                    var user = await UserManager.FindByIdAsync(User.Identity.GetUserId<int>());
                    if (user != null)
                    {
                        await SignInManager.SignInAsync(user, isPersistent: false, rememberBrowser: false);
                    }
                    return RedirectToAction("Index", new { Message = ManageMessageId.SetPasswordSuccess });
                }
                AddErrors(result);
            }

            // If we got this far, something failed, redisplay form
            return View(model);
        }

        //
        // GET: /Manage/ManageLogins
        public async Task<ActionResult> ManageLogins(ManageMessageId? message)
        {
            ViewBag.StatusMessage =
                message == ManageMessageId.RemoveLoginSuccess ? "The external login was removed."
                : message == ManageMessageId.Error ? "An error has occurred."
                : "";
            var user = await UserManager.FindByIdAsync(User.Identity.GetUserId<int>());
            if (user == null)
            {
                return View("Error");
            }
            var userLogins = await UserManager.GetLoginsAsync(User.Identity.GetUserId<int>());
            var otherLogins = AuthenticationManager.GetExternalAuthenticationTypes().Where(auth => userLogins.All(ul => auth.AuthenticationType != ul.LoginProvider)).ToList();
            ViewBag.ShowRemoveButton = user.PasswordHash != null || userLogins.Count > 1;
            return View(new ManageLoginsViewModel
            {
                CurrentLogins = userLogins,
                OtherLogins = otherLogins
            });
        }

        //
        // POST: /Manage/LinkLogin
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult LinkLogin(string provider)
        {
            // Request a redirect to the external login provider to link a login for the current user
            return new AccountController.ChallengeResult(provider, Url.Action("LinkLoginCallback", "Manage"), User.Identity.GetUserId());
        }

        //
        // GET: /Manage/LinkLoginCallback
        public async Task<ActionResult> LinkLoginCallback()
        {
            var loginInfo = await AuthenticationManager.GetExternalLoginInfoAsync(XsrfKey, User.Identity.GetUserId());
            if (loginInfo == null)
            {
                return RedirectToAction("ManageLogins", new { Message = ManageMessageId.Error });
            }
            var result = await UserManager.AddLoginAsync(User.Identity.GetUserId<int>(), loginInfo.Login);
            return result.Succeeded ? RedirectToAction("ManageLogins") : RedirectToAction("ManageLogins", new { Message = ManageMessageId.Error });
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _userManager != null)
            {
                _userManager.Dispose();
                _userManager = null;
            }

            base.Dispose(disposing);
        }
        //GET: /Manage/AddCategories
        [HttpGet]
        public ActionResult AddCategories()
        {
            return View(db.Categories.ToList());
        }
        //private List<Category> categoryList = new kapymvc1Entities().Categories.ToList();
        [HttpPost, ActionName("Insert")]
        public ActionResult AddCategories(IEnumerable<Category> categories)
        {
            if (categories.Count(x => x.isSelected == true) == 0)
            {
                ViewBag.message = "You didnt select any categories";
            }
            else {
                StringBuilder sb = new StringBuilder();
                sb.Append("You selected -");
                foreach (Category c in categories)
                {
                    if (c.isSelected == true)
                    {
                        sb.Append(c.categoryName + " ,");
                        if (ModelState.IsValid)
                        {
                            var save = new AspNetUser_Category
                            {
                                userId = User.Identity.GetUserId<int>(),
                                categoryId = c.categoryId
                            };
                            db.AspNetUser_Category.Add(save);
                            db.SaveChanges();
                        }
                        ModelState.Clear();

                    }
                }
                sb.Remove(sb.ToString().LastIndexOf(","), 1);
                ViewBag.message = sb.ToString();
            }
            return View();
        }
        public ActionResult UserProfile()
        {
            return View();

        }

        //public ActionResult ShowCurrentCategories()
        //{

        //    int userId = User.Identity.GetUserId<int>();
        //    if (userId == 0)
        //    {
        //        return RedirectToAction("Login", "Account");
        //    }

        //    var currentSelectedCategories = db.Categories.Where(u => u.AspNetUser_Category.Any(a => a.userId == userId));
        //    IEnumerable<Category> currentCategories = currentSelectedCategories.ToList();

        //    StringBuilder sb = new StringBuilder();
        //    sb.Append("Your current favorite categories : ");
        //    List<int> ids = new List<int>();
        //    foreach (var c in currentCategories)
        //    {
        //        sb.Append(c.categoryName + "\n");
        //        ids.Add(c.categoryId);
        //    }

        //    return View(currentCategories);
        //}

        //[HttpGet]
        //[AllowAnonymous]
        //public ActionResult UpdatePicture()
        //{
        //    return View();
        //}
        [Authorize]

        public ActionResult NotificationSetting()
        {
            int userId = User.Identity.GetUserId<int>();
            if (userId == 0)
            {
                return RedirectToAction("Login", "Account");
            }
            else {
                ApplicationUser databaseUser = this.UserManager.FindById(userId);
                ViewBag.Message = databaseUser.isNotified.ToString();
            }
            //AspNetUser aspNetUser = db.AspNetUsers.Find(userId);
            return View();

        }

        [Authorize]
        [HttpPost, ActionName("ReceiveNotification")]
        [ValidateAntiForgeryToken]
        //public ActionResult ReceiveNotification([Bind(Include = "Id,Email,isNotified")] AspNetUser aspNetUser)
        public ActionResult ReceiveNotification()
        {
            int id=User.Identity.GetUserId<int>();
            //AspNetUser aspNetUser = db.AspNetUsers.Find(userId);
            if (ModelState.IsValid && id!=0)
            {
                ApplicationUser databaseUser = this.UserManager.FindById(id);
                databaseUser.isNotified = 1;
                IdentityResult result = this.UserManager.Update(databaseUser);
                //db.SaveChanges();
                if (result.Succeeded)
                {
                    ViewBag.Title = "success";
                }
                else
                {
                    this.AddErrors(result);
                    return RedirectToAction("Index", "Home");
                }
            }
            ModelState.Clear();
            return RedirectToAction("NotificationSetting", "Manage");

        }

        [Authorize]
        [HttpPost, ActionName("NoNotification")]
        [ValidateAntiForgeryToken]
        public ActionResult NoNotification()
        {
            int id = User.Identity.GetUserId<int>();
            //AspNetUser aspNetUser = db.AspNetUsers.Find(userId);
            if (ModelState.IsValid && id != 0)
            {
                ApplicationUser databaseUser = this.UserManager.FindById(id);
                databaseUser.isNotified = 0;
                IdentityResult result = this.UserManager.Update(databaseUser);
                //db.SaveChanges();
                if (result.Succeeded)
                {
                    ViewBag.Title = "success";
                }
                else
                {
                    this.AddErrors(result);
                    return RedirectToAction("Index", "Home");
                }
            }
            ModelState.Clear();
            return RedirectToAction("NotificationSetting", "Manage");

        }
        //[HttpGet]
        //public ActionResult UpdatePicture() {
        //    return View();
        //}
        //public ActionResult PicModify()
        //{
        //    int userId = User.Identity.GetUserId<int>();
        //    if (userId == 0)
        //    {
        //        return RedirectToAction("Login", "Account");
        //    }
        //    else {

        //        ViewBag.Message = "updating pictures";
        //    }
        //    //AspNetUser aspNetUser = db.AspNetUsers.Find(userId);
        //    return View();

        //}
        //[HttpPost]
        //[AllowAnonymous]
        //[ValidateAntiForgeryToken]
        //public ActionResult UpdatePicture()
        //{
        //    int id = User.Identity.GetUserId<int>();
        //    if (ModelState.IsValid && id!=0)
        //    {
        //        byte[] imageData = null;
        //        if (Request.Files.Count > 0)
        //        {
        //            HttpPostedFileBase poImgFile = Request.Files["UserPhoto"];

        //            using (var binary = new BinaryReader(poImgFile.InputStream))
        //            {
        //                imageData = binary.ReadBytes(poImgFile.ContentLength);
        //                ViewBag.Message = imageData;
        //            }
        //            ApplicationUser databaseUser = this.UserManager.FindById(id);
        //            databaseUser.UserPhoto = imageData;
        //            IdentityResult result = this.UserManager.Update(databaseUser);

        //            if (result.Succeeded)
        //            {
        //                return RedirectToAction("Index", "Manage");
        //            }
        //            else
        //            {
        //                this.AddErrors(result);
        //                return RedirectToAction("Index", "Home");
        //            }
        //        } //close count if



        //    }
        //    ModelState.Clear();
        //    return RedirectToAction("Index", "Manage");
        //}


        ////
        //// POST: /Manage/UpdatePicture
        //[HttpPost]
        //[ValidateAntiForgeryToken]

        ////public async Task<ActionResult> UpdatePicture()
        //public async Task<ActionResult> UpdatePicture(UpdatePictureViewModel model)
        //{
        //    if (ModelState.IsValid)
        //    {
        //        byte[] imageData = null;
        //        if (Request.Files.Count > 0)
        //        {
        //            HttpPostedFileBase poImgFile = Request.Files["UserPhoto"];

        //            using (var binary = new BinaryReader(poImgFile.InputStream))
        //            {
        //                imageData = binary.ReadBytes(poImgFile.ContentLength);
        //            }
        //        } //close count if


        //        //Pass the byte array to the user context to store in database
        //        var user = await UserManager.FindByIdAsync(User.Identity.GetUserId<int>());
        //        user.UserPhoto = imageData;

        //        if (user != null)
        //        {
        //            await SignInManager.SignInAsync(user, isPersistent: false, rememberBrowser: false);
        //        }
        //        return RedirectToAction("Index", "Manage");
        //    }

        //    return RedirectToAction("Index", "Manage");
        //}








        //[HttpPost]
        //[AllowAnonymous]
        //[ValidateAntiForgeryToken]
        //public async Task<ActionResult> UpdatePicture()
        //{
        //    return View();
        //}



        //

        #region Helpers
        // Used for XSRF protection when adding external logins
        private const string XsrfKey = "XsrfId";

        private IAuthenticationManager AuthenticationManager
        {
            get
            {
                return HttpContext.GetOwinContext().Authentication;
            }
        }

        private void AddErrors(IdentityResult result)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("", error);
            }
        }

        private bool HasPassword()
        {
            var user = UserManager.FindById(User.Identity.GetUserId<int>());
            if (user != null)
            {
                return user.PasswordHash != null;
            }
            return false;
        }

        private bool HasPhoneNumber()
        {
            var user = UserManager.FindById(User.Identity.GetUserId<int>());
            if (user != null)
            {
                return user.PhoneNumber != null;
            }
            return false;
        }

        public enum ManageMessageId
        {
            AddPhoneSuccess,
            ChangePasswordSuccess,
            SetTwoFactorSuccess,
            SetPasswordSuccess,
            RemoveLoginSuccess,
            RemovePhoneSuccess,
            Error
        }



        // GET: /Account/Register
        [AllowAnonymous]
        public ActionResult UpdatePicture()
        {
            return View();
        }


       [HttpPost]
       [AllowAnonymous]
       [ValidateAntiForgeryToken]

        public async Task<ActionResult> UpdatePicture([Bind(Exclude = "UserPhoto")]RegisterViewModel model)
        //public async Task<ActionResult> Register(RegisterViewModel model)
        {
            if (ModelState.IsValid)
            {
                byte[] imageData = null;
                if (Request.Files.Count > 0)
                {
                    HttpPostedFileBase poImgFile = Request.Files["UserPhoto"];

                    using (var binary = new BinaryReader(poImgFile.InputStream))
                    {
                        imageData = binary.ReadBytes(poImgFile.ContentLength);
                    }
                } //close count if


                //////
                var user = new ApplicationUser { UserName = model.Email, Email = model.Email };

                //Pass the byte array to the user context to store in database

                user.UserPhoto = imageData;

                var result = await UserManager.CreateAsync(user, model.Password);
                if (result.Succeeded)
                {

                    await SignInManager.SignInAsync(user, isPersistent: false, rememberBrowser: false);


                    return View("Info");

                    //   return RedirectToAction("Insert", "AspNetUser_Category");
                    //return RedirectToAction("AddCategories", "Manage");
                }

                AddErrors(result);
            }

            // If we got this far, something failed, redisplay form
            return View(model);
        }








        #endregion
    }
}