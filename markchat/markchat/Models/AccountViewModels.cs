using System;
using System.Collections.Generic;

namespace markchat.Models
{
    public enum ResponceCode
    {
        OK = 0,
        UserDeleted,
        BadRequest,

        UserDosentExist,
        RequestDosentExist,
        ChatDoesntExist,
        RequestDoesntExist,
        CategoryDoesntExist,
        RequestAlreadyExistsInUser,
        UserAlreadyExistsInChat,
        NotChatMember,
        PremissionError,
        UserAlreadyConfirmed,
        NotOwnerChat,
        CategoryNameAlreadyExist,
        CategoryNameEmpty,
        PhoneNumberAlreadyExist,
        PhoneNumberAlreadyConfirmed,
        PhoneNumberExceededLimit,
        CodeIsNotCorrect,
        InvalidToken,
        InvalidEmail,
        FullNameChanged,
        EmailChanged,
        EmailAlreadyConfirmed
    }
    public class GetLastMessagesModel
    {
        public int TagChatId { get; set; }
    }

    public class IsChatOwnerModel
    {
        public int TagChatId { get; set; }
    }

    public class GetNextMessagesModel
    {
        public int TagChatId { get; set; }
        public int LastNotificationId { get; set; }
    }

    public class InvitationRequestFromUserModel
    {
        public int TagChatId { get; set; }
    }
    public class InvitationRequestsToUsersModel
    {
        public int TagChatId { get; set; }
        public List<string> UsersId { get; set; }
    }

    public class ChatInfoModel
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }
    public class InvitationRequestFromTagChatModel
    {
        public int TagChatId { get; set; }
        public string UserId { get; set; }
    }
    public class AcceptInvitationRequestModel
    {
        public int InvRequestId { get; set; }
    }

    public class DenyInvitationRequestModel
    {
        public int InvRequestId { get; set; }
    }
    public class GetRootCategoryByTagChatIdModel
    {
        public int TagChatId { get; set; }
    }
    public class CreateSubCategoryModel
    {
        public int ParentCatId { get; set; }
        public string NameNewCat { get; set; }
        public string TitleNewCat { get; set; }
    }
    
    public class TagChatModel
    {
        public int Id { get; set; }
        public string Name{ get; set; }
        public string OwnerUserId{ get; set; }
        public string OwnerUserName { get; set; }
        public int RootCategoryId { get; set; }
        public string RootCategoryName { get; set; }
    }

    public class CurrencyModel
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public char Symbol { get; set; }
    }
    public class GetChatUsersModel
    {
        public int TagChatId { get; set; }
    }
    public class ListTagChatsByNameModel
    {
        public string TagChatName { get; set; }
    }
    public class ListUsersTagChatModel
    {
        public int IdTagChat { get; set; }
    }
    
    public class GetMemberModel
    {
        public string UserId { get; set; }
        public string FullName { get; set; }
        public string PhotoName { get; set; }
        public string Photo { get; set; }
    }
    
    public class ChatUserModel
    {
        public string UserId { get; set; }
        public string UserName { get; set; }
      //  public string UserPhotoName { get; set; }
        public string UserPhoneNumber { get; set; }
    }

    public class NewRequestsFromChatsToUserModel
    {
        public int TagChatId { get; set; }
        public string TagChatName { get; set; }
        public string OwnerId { get; set; }
        public string OwnerName { get; set; }
       // public string OwnerPhotoName { get; set; }
        public string OwnerPhoneNumber { get; set; }
    }

    public class NewRequestsFromUsersToChatModel
    {
        public int TagChatId { get; set; }
        public string TagChatName { get; set; }
        public string UserId { get; set; }
        public string UserName { get; set; }
        //public string UserPhotoName{ get; set; }
        public string UserPhoneNumber{ get; set; }
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
