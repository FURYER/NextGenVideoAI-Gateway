// ApiGateway/GraphQL/Mutation.cs
using HotChocolate.Authorization;
using Microsoft.AspNetCore.Authorization;
using HotChocolate.Types;
using ApiGateway.Services;

namespace ApiGateway.GraphQL
{
    public class Mutation
    {
        [HotChocolate.Authorization.Authorize(Policy = "AllowAnonymous")]
        public async Task<AuthResponse> RegisterUser([Service] UserResolver resolver, RegisterUserInput input)
        {
            return await resolver.RegisterUser(input);
        }

        [HotChocolate.Authorization.Authorize(Policy = "AllowAnonymous")]
        public async Task<AuthResponse> Login([Service] UserResolver resolver, LoginInput input)
        {
            return await resolver.Login(input);
        }

        [HotChocolate.Authorization.Authorize(Policy = "AllowAnonymous")]
        public async Task<AuthResponse> RefreshToken([Service] UserResolver resolver, string refreshToken)
        {
            return await resolver.RefreshToken(refreshToken);
        }

        [HotChocolate.Authorization.Authorize]
        public async Task<Chat> CreateChat([Service] ChatResolver resolver, string userId)
        {
            return await resolver.CreateChat(userId);
        }

        [HotChocolate.Authorization.Authorize]
        public async Task<ChatMessage?> SendMessage([Service] ChatResolver resolver, string chatId, string content, string userId,
            bool requiresEditing = false, bool isEditingStage = false, List<Attachment>? attachments = null, List<string>? selectedFileIds = null)
        {
            return await resolver.SendMessage(chatId, content, userId, requiresEditing, isEditingStage, attachments, selectedFileIds);
        }

        [HotChocolate.Authorization.Authorize]
        public async Task<bool> Logout([Service] UserResolver resolver, [GlobalState] string? accessToken)
        {
            return await resolver.Logout(accessToken);
        }

        [HotChocolate.Authorization.Authorize]
        public async IAsyncEnumerable<ChatStreamResponse> StreamChat([Service] ChatResolver resolver, StreamChatInput input)
        {
            await foreach (var response in resolver.StreamChat(input))
            {
                yield return response;
            }
        }

        [HotChocolate.Authorization.Authorize]
        public async Task<Attachment> AttachFileToMessage([Service] ChatResolver resolver, string messageId, Attachment attachment)
        {
            return await resolver.AttachFileToMessage(messageId, attachment);
        }

        [HotChocolate.Authorization.Authorize]
        public async Task<bool> RemoveAttachedFile([Service] ChatResolver resolver, string messageId, string attachmentId)
        {
            return await resolver.RemoveAttachedFile(messageId, attachmentId);
        }

        // Методы для работы с файловым хранилищем
        [HotChocolate.Authorization.Authorize]
        public async Task<UploadFileResult> UploadFile([Service] StorageResolver resolver, UploadFileInput input)
        {
            var response = await resolver.UploadFile(
                input.UserId,
                input.FileName,
                input.ContentType,
                input.FileData,
                input.ChatId);

            return new UploadFileResult
            {
                Success = response.Success,
                ErrorMessage = response.ErrorMessage,
                FileId = response.FileId,
                FileSize = response.FileSize
            };
        }

        [HotChocolate.Authorization.Authorize]
        public async Task<DeleteFileResult> DeleteFile([Service] StorageResolver resolver, string fileId)
        {
            var response = await resolver.DeleteFile(fileId);

            return new DeleteFileResult
            {
                Success = response.Success,
                ErrorMessage = response.ErrorMessage
            };
        }

        // Методы для фрагментированной загрузки файлов
        [HotChocolate.Authorization.Authorize]
        public async Task<InitChunkedUploadResult> InitChunkedUpload([Service] StorageResolver resolver, InitChunkedUploadInput input)
        {
            return await resolver.InitChunkedUpload(input);
        }

        [HotChocolate.Authorization.Authorize]
        public async Task<ChunkUploadResult> UploadFileChunk([Service] StorageResolver resolver, ChunkUploadInput input)
        {
            return await resolver.UploadFileChunk(input);
        }

        [HotChocolate.Authorization.Authorize]
        public async Task<CompleteChunkedUploadResult> CompleteChunkedUpload([Service] StorageResolver resolver, string uploadId, string userId)
        {
            return await resolver.CompleteChunkedUpload(uploadId, userId);
        }

        [HotChocolate.Authorization.Authorize]
        public async Task<CancelChunkedUploadResult> CancelChunkedUpload([Service] StorageResolver resolver, string uploadId, string userId)
        {
            return await resolver.CancelChunkedUpload(uploadId, userId);
        }

        [HotChocolate.Authorization.Authorize]
        public async Task<UpdateChatPayload> UpdateChat([Service] ChatResolver resolver, UpdateChatInput input)
        {
            try
            {
                var chat = await resolver.UpdateChat(input);
                return new UpdateChatPayload
                {
                    Success = true,
                    Chat = chat
                };
            }
            catch (Exception ex)
            {
                return new UpdateChatPayload
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        [HotChocolate.Authorization.Authorize]
        public async Task<bool> StopMontage(
            [Service] IRabbitMQService rabbitMQService,
            string userId,
            string chatId,
            string? messageId = null)
        {
            await rabbitMQService.PublishMontageStopCommandAsync(userId, chatId, messageId);
            return true;
        }

        [HotChocolate.Authorization.Authorize]
        public async Task<EmailCodePayload> RequestEmailCode([Service] UserResolver resolver, EmailCodeInput input)
        {
            return await resolver.RequestEmailCode(input);
        }

        [HotChocolate.Authorization.Authorize]
        public async Task<EmailCodePayload> ConfirmEmailCode([Service] UserResolver resolver, ConfirmEmailCodeInput input)
        {
            return await resolver.ConfirmEmailCode(input);
        }

        [HotChocolate.Authorization.Authorize]
        public async Task<CreatePaymentPayload> CreatePayment([Service] PaymentResolver resolver, CreatePaymentInput input)
        {
            return await resolver.CreatePayment(input);
        }

        [HotChocolate.Authorization.Authorize]
        public async Task<PaymentWebhookPayload> PaymentWebhook([Service] PaymentResolver resolver, PaymentWebhookInput input)
        {
            return await resolver.PaymentWebhook(input);
        }

        [HotChocolate.Authorization.Authorize]
        public async Task<UseDemoQuotaPayload> UseDemoQuota([Service] PaymentResolver resolver, string userId)
        {
            return await resolver.UseDemoQuota(userId);
        }
    }
}