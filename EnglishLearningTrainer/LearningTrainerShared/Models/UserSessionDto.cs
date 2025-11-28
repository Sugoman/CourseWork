using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;

namespace LearningTrainerShared.Models
{
    public class UserSessionDto
    {
        public string AccessToken { get; set; }
        public string UserLogin { get; set; }
        public string UserRole { get; set; }
        public int UserId { get; set; }      
        public string InviteCode { get; set; }
    }
}
