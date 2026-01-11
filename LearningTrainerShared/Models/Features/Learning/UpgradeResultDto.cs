using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LearningTrainerShared.Models
{
    public class UpgradeResultDto
    {
        public string Message { get; set; }
        public string InviteCode { get; set; }
        public string AccessToken { get; set; }
        public string UserRole { get; set; }
    }
}
