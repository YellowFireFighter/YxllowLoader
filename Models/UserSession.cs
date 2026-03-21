using System;

namespace YxllowLoader.Models
{
    public class UserSession
    {
        public string Username { get; set; }
        public string Plan { get; set; }   // "Basic" | "Pro" | "Lifetime"
        public DateTime ExpiresAt { get; set; }
        public string HwidStatus { get; set; }   // "Verified" | "Unverified"
        public string AvatarColor { get; set; }   // hex fallback color

        public string ExpiryDisplay =>
            Plan == "Lifetime" ? "Never" : ExpiresAt.ToString("MMM dd, yyyy");

        public bool IsExpired =>
            Plan != "Lifetime" && ExpiresAt < DateTime.UtcNow;

        public string DaysLeft =>
            Plan == "Lifetime" ? "∞" :
            IsExpired ? "Expired" :
            $"{(ExpiresAt - DateTime.UtcNow).Days}d left";
    }
}