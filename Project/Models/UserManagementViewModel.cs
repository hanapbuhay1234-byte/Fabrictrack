using System.ComponentModel.DataAnnotations;

namespace FabriTrack.Models
{
    public class UserManagementViewModel
    {
        public string? Search { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public int TotalStaff { get; set; }
        public int ActiveStaff { get; set; }
        public int InactiveStaff { get; set; }
        public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalStaff / (double)PageSize));
        public bool HasPreviousPage => Page > 1;
        public bool HasNextPage => Page < TotalPages;
        public List<StaffUserRowViewModel> Staff { get; set; } = new();
        public CreateStaffViewModel CreateStaff { get; set; } = new();
        public EditStaffViewModel EditStaff { get; set; } = new();
        public bool OpenCreateModal { get; set; }
        public bool OpenEditModal { get; set; }
        public string? TemporaryPassword { get; set; }
    }

    public class StaffUserRowViewModel
    {
        public string Id { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public bool RequirePasswordChange { get; set; }
    }

    public class CreateStaffViewModel
    {
        [Required, StringLength(100)]
        public string FullName { get; set; } = string.Empty;

        [Required, EmailAddress, StringLength(256)]
        public string Email { get; set; } = string.Empty;
    }

    public class EditStaffViewModel
    {
        [Required]
        public string Id { get; set; } = string.Empty;

        [Required, StringLength(100)]
        public string FullName { get; set; } = string.Empty;

        [Required, EmailAddress, StringLength(256)]
        public string Email { get; set; } = string.Empty;
    }
}
