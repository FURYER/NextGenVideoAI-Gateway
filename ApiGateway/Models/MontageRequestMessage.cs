using System;
using System.Collections.Generic;

namespace OrchestrationService.Domain.Events
{
    public class MontageRequestMessage
    {
        public Guid Id { get; set; }
        public string Content { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public SenderType SenderType { get; set; }
        public string SenderId { get; set; } = string.Empty;
        public Guid ChatId { get; set; }
        public string Chat { get; set; } = string.Empty;
        public bool RequiresEditing { get; set; }
        public bool IsEditingStage { get; set; }
        public string? CommandId { get; set; }
        public bool IsCompleted { get; set; }
        public bool Canceled { get; set; }
        public ICollection<MessageAttachmentDto> Attachments { get; set; } = new List<MessageAttachmentDto>();
        public ICollection<string> SelectedFileIds { get; set; } = new List<string>();
    }

    public class MessageAttachmentDto
    {
        public string FileId { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
    }

    public enum SenderType
    {
        USER = 0,
        AI = 1,
        AI2 = 2
    }
}