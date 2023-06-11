using Microsoft.EntityFrameworkCore;

namespace QuestionsApp.Web.DB
{
    public class QuestionsContext : DbContext
    {
        public QuestionsContext(DbContextOptions options) : base(options)
        { }

        public DbSet<QuestionDB> Questions { get; set; }
        public DbSet<VoteDB> Votes { get; set; }
    }
}
