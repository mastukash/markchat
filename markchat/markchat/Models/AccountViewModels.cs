using System;
using System.Collections.Generic;

namespace markchat.Models
{
    public class GetLastMessagesModel
    {
        public int TagChatId { get; set; }
    }

    public class GetRootCategoriesModel
    {
        public int TagChatId { get; set; }
    }

    public class GetChildCategoriesById
    {
        public int ParentCategoryId { get; set; }
    }

    public class PhoneConfirnationModel
    {
        public string Token { get; set; }
        public string Code { get; set; }
    }

    public class ConfirmEmailModel
    {
        public string Code { get; set; }
    }

    public class ChangeFullNameModel
    {
        public string FullName { get; set; }
    }


    public class ChangePhotoModel
    {
        public string PhotoName { get; set; }
        public byte[] File { get; set; }
    }

    // Models returned by AccountController actions.

    public class ExternalLoginViewModel
    {
        public string Name { get; set; }

        public string Url { get; set; }

        public string State { get; set; }
    }

    public class ManageInfoViewModel
    {
        public string LocalLoginProvider { get; set; }

        public string Email { get; set; }

        public IEnumerable<UserLoginInfoViewModel> Logins { get; set; }

        public IEnumerable<ExternalLoginViewModel> ExternalLoginProviders { get; set; }
    }

    public class UserInfoViewModel
    {
        public string Email { get; set; }

        public bool HasRegistered { get; set; }

        public string LoginProvider { get; set; }
    }

    public class UserLoginInfoViewModel
    {
        public string LoginProvider { get; set; }

        public string ProviderKey { get; set; }
    }
}
