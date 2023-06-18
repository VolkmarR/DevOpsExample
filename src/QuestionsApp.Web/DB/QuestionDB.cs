using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace QuestionsApp.Web.DB
{
    public class QuestionDB
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ID { get; set; }
        public string Content { get; set; } = "";
        public ICollection<VoteDB> Votes { get; set; } = null!;
    }

}
