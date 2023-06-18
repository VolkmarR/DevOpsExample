using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace QuestionsApp.Web.DB
{
    public class VoteDB
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ID { get; set; }
        public int QuestionID { get; set; }
        public QuestionDB Question { get; set; } = null!;
    }

}
