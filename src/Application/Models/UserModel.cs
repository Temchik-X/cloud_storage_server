using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Models
{
    public class UserModel
    {
        [Key]
        public int Id { get; set; }
        public string Email { get; set; }
        public string Username { get; set; }
        public string PasswordHash { get; set; }
        public bool IsAdmin { get; set; }
        public int FreeSpace { get; set; }
        public DateTime DateRegistration { get; set; }

    }
}
