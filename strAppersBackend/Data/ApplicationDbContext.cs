using Microsoft.EntityFrameworkCore;
using strAppersBackend.Models;

namespace strAppersBackend.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<Student> Students { get; set; }
    public DbSet<Organization> Organizations { get; set; }
    public DbSet<Project> Projects { get; set; }
    public DbSet<ProjectStatus> ProjectStatuses { get; set; }
    public DbSet<ProjectCriteria> ProjectCriterias { get; set; }
    public DbSet<Role> Roles { get; set; }
    public DbSet<StudentRole> StudentRoles { get; set; }
    public DbSet<Major> Majors { get; set; }
    public DbSet<Year> Years { get; set; }
    public DbSet<JoinRequest> JoinRequests { get; set; }
    public DbSet<DesignVersion> DesignVersions { get; set; }
    public DbSet<ProjectBoard> ProjectBoards { get; set; }
    public DbSet<BoardMeeting> BoardMeetings { get; set; }
        public DbSet<ModuleType> ModuleTypes { get; set; }
        public DbSet<ProjectModule> ProjectModules { get; set; }
        public DbSet<Figma> Figma { get; set; }
        public DbSet<ProgrammingLanguage> ProgrammingLanguages { get; set; }
        public DbSet<ProjectsIDE> ProjectsIDE { get; set; }
        public DbSet<Subscription> Subscriptions { get; set; }
        public DbSet<Employer> Employers { get; set; }
        public DbSet<EmployerBoard> EmployerBoards { get; set; }
        public DbSet<EmployerAdd> EmployerAdds { get; set; }
        public DbSet<EmployerCandidate> EmployerCandidates { get; set; }
        public DbSet<AIModel> AIModels { get; set; }
        public DbSet<MentorChatHistory> MentorChatHistory { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure Major entity
        modelBuilder.Entity<Major>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.Department).HasMaxLength(50);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            // Seed test data
            entity.HasData(
                new Major { Id = 1, Name = "Computer Science", Description = "Study of computational systems and design", Department = "Computer Science", IsActive = true, CreatedAt = DateTime.UtcNow.AddDays(-60) },
                new Major { Id = 2, Name = "Software Engineering", Description = "Engineering approach to software development", Department = "Computer Science", IsActive = true, CreatedAt = DateTime.UtcNow.AddDays(-60) },
                new Major { Id = 3, Name = "Data Science", Description = "Extracting insights from data", Department = "Computer Science", IsActive = true, CreatedAt = DateTime.UtcNow.AddDays(-60) },
                new Major { Id = 4, Name = "Cybersecurity", Description = "Protecting digital systems and data", Department = "Computer Science", IsActive = true, CreatedAt = DateTime.UtcNow.AddDays(-60) },
                new Major { Id = 5, Name = "Information Technology", Description = "Management and use of technology", Department = "Information Systems", IsActive = true, CreatedAt = DateTime.UtcNow.AddDays(-60) },
                new Major { Id = 6, Name = "Business Administration", Description = "General business management", Department = "Business", IsActive = true, CreatedAt = DateTime.UtcNow.AddDays(-60) }
            );
        });

        // Configure Year entity
        modelBuilder.Entity<Year>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Description).HasMaxLength(200);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            // Seed test data
            entity.HasData(
                new Year { Id = 1, Name = "Freshman", Description = "First year of study", SortOrder = 1, IsActive = true, CreatedAt = DateTime.UtcNow.AddDays(-60) },
                new Year { Id = 2, Name = "Sophomore", Description = "Second year of study", SortOrder = 2, IsActive = true, CreatedAt = DateTime.UtcNow.AddDays(-60) },
                new Year { Id = 3, Name = "Junior", Description = "Third year of study", SortOrder = 3, IsActive = true, CreatedAt = DateTime.UtcNow.AddDays(-60) },
                new Year { Id = 4, Name = "Senior", Description = "Fourth year of study", SortOrder = 4, IsActive = true, CreatedAt = DateTime.UtcNow.AddDays(-60) },
                new Year { Id = 5, Name = "Graduate", Description = "Graduate level study", SortOrder = 5, IsActive = true, CreatedAt = DateTime.UtcNow.AddDays(-60) }
            );
        });

        // Configure Organization entity
        modelBuilder.Entity<Organization>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.Website).HasMaxLength(100);
            entity.Property(e => e.ContactEmail).HasMaxLength(255);
            entity.Property(e => e.Phone).HasMaxLength(20);
            entity.Property(e => e.Address).HasMaxLength(200);
            entity.Property(e => e.Type).HasMaxLength(50);
            entity.Property(e => e.Logo).HasColumnName("Logo").HasColumnType("text");
            entity.Property(e => e.TermsUse).HasColumnType("text");
            entity.Property(e => e.TermsAccepted).HasDefaultValue(false);
            entity.Property(e => e.TermsAcceptedAt).HasColumnType("timestamp with time zone");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            // Seed test data
            entity.HasData(
                new Organization { Id = 1, Name = "Tech University", Description = "Leading technology university", Website = "https://techuniversity.edu", ContactEmail = "info@techuniversity.edu", Phone = "555-0101", Address = "123 Tech Street, Tech City", Type = "University", IsActive = true, CreatedAt = DateTime.UtcNow.AddDays(-60) },
                new Organization { Id = 2, Name = "Innovation Labs", Description = "Research and development company", Website = "https://innovationlabs.com", ContactEmail = "contact@innovationlabs.com", Phone = "555-0102", Address = "456 Innovation Ave, Tech City", Type = "Company", IsActive = true, CreatedAt = DateTime.UtcNow.AddDays(-55) },
                new Organization { Id = 3, Name = "Code for Good", Description = "Non-profit organization promoting tech for social good", Website = "https://codeforgood.org", ContactEmail = "hello@codeforgood.org", Phone = "555-0103", Address = "789 Good Street, Tech City", Type = "Non-profit", IsActive = true, CreatedAt = DateTime.UtcNow.AddDays(-50) }
            );
        });

        // Configure ProjectStatus entity
        modelBuilder.Entity<ProjectStatus>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Description).HasMaxLength(200);
            entity.Property(e => e.Color).HasMaxLength(7);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            // Seed test data
            entity.HasData(
                new ProjectStatus { Id = 1, Name = "New", Description = "Newly created project", Color = "#10B981", SortOrder = 1, IsActive = true, CreatedAt = DateTime.UtcNow.AddDays(-40) },
                new ProjectStatus { Id = 2, Name = "Planning", Description = "Project in planning phase", Color = "#3B82F6", SortOrder = 2, IsActive = true, CreatedAt = DateTime.UtcNow.AddDays(-40) },
                new ProjectStatus { Id = 3, Name = "In Progress", Description = "Project currently being worked on", Color = "#F59E0B", SortOrder = 3, IsActive = true, CreatedAt = DateTime.UtcNow.AddDays(-40) },
                new ProjectStatus { Id = 4, Name = "On Hold", Description = "Project temporarily paused", Color = "#EF4444", SortOrder = 4, IsActive = true, CreatedAt = DateTime.UtcNow.AddDays(-40) },
                new ProjectStatus { Id = 5, Name = "Completed", Description = "Project successfully completed", Color = "#059669", SortOrder = 5, IsActive = true, CreatedAt = DateTime.UtcNow.AddDays(-40) },
                new ProjectStatus { Id = 6, Name = "Cancelled", Description = "Project cancelled or abandoned", Color = "#6B7280", SortOrder = 6, IsActive = true, CreatedAt = DateTime.UtcNow.AddDays(-40) }
            );
        });

        // Configure ProjectCriteria entity
        modelBuilder.Entity<ProjectCriteria>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);

            // Seed initial data
            entity.HasData(
                new ProjectCriteria { Id = 1, Name = "Popular Projects" },
                new ProjectCriteria { Id = 2, Name = "UI/UX Designer Needed" },
                new ProjectCriteria { Id = 3, Name = "Backend Developer Needed" },
                new ProjectCriteria { Id = 4, Name = "Frontend Developer Needed" },
                new ProjectCriteria { Id = 5, Name = "Product manager Needed" },
                new ProjectCriteria { Id = 6, Name = "Marketing Needed" },
                new ProjectCriteria { Id = 7, Name = "New Projects" }
            );
        });

        // Configure Student entity
        modelBuilder.Entity<Student>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FirstName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.LastName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Email).IsRequired().HasMaxLength(255);
            entity.Property(e => e.StudentId).HasMaxLength(255);
            entity.Property(e => e.LinkedInUrl).IsRequired().HasMaxLength(200);
            entity.Property(e => e.GithubUser).IsRequired().HasMaxLength(255);
            entity.Property(e => e.Photo).HasColumnName("Photo").HasColumnType("text");
            entity.Property(e => e.StartPendingAt).HasColumnType("timestamp with time zone");
            entity.Property(e => e.ProgrammingLanguageId).HasColumnName("ProgrammingLanguageId");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            
            entity.HasIndex(e => e.Email).IsUnique();
            entity.HasIndex(e => e.StudentId).IsUnique();

            // Foreign key relationships
            entity.HasOne(e => e.Major)
                  .WithMany(m => m.Students)
                  .HasForeignKey(e => e.MajorId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Year)
                  .WithMany(y => y.Students)
                  .HasForeignKey(e => e.YearId)
                  .OnDelete(DeleteBehavior.Restrict);

            // Organization relationship removed - Students no longer have OrganizationId

            // Direct relationship with Project (student can be allocated to one project)
            entity.HasOne(e => e.Project)
                  .WithMany()
                  .HasForeignKey(e => e.ProjectId)
                  .OnDelete(DeleteBehavior.SetNull);

            // Preferred projects relationships (nullable FKs to Projects)
            entity.HasOne(e => e.ProjectPriority1Project)
                  .WithMany()
                  .HasForeignKey(e => e.ProjectPriority1)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.ProjectPriority2Project)
                  .WithMany()
                  .HasForeignKey(e => e.ProjectPriority2)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.ProjectPriority3Project)
                  .WithMany()
                  .HasForeignKey(e => e.ProjectPriority3)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.ProjectPriority4Project)
                  .WithMany()
                  .HasForeignKey(e => e.ProjectPriority4)
                  .OnDelete(DeleteBehavior.SetNull);

            // Foreign key relationship to ProjectBoard (Trello board)
            entity.HasOne(e => e.ProjectBoard)
                  .WithMany()
                  .HasForeignKey(e => e.BoardId)
                  .OnDelete(DeleteBehavior.SetNull);

            // Foreign key relationship to ProgrammingLanguage
            entity.HasOne(e => e.ProgrammingLanguage)
                  .WithMany(pl => pl.Students)
                  .HasForeignKey(e => e.ProgrammingLanguageId)
                  .OnDelete(DeleteBehavior.SetNull);

            // Seed test data
            entity.HasData(
                new Student { Id = 1, FirstName = "Alex", LastName = "Johnson", Email = "alex.johnson@techuniversity.edu", StudentId = "TU001", MajorId = 1, YearId = 3, LinkedInUrl = "https://linkedin.com/in/alexjohnson", IsAdmin = true, CreatedAt = DateTime.UtcNow.AddDays(-45) },
                new Student { Id = 2, FirstName = "Sarah", LastName = "Williams", Email = "sarah.williams@techuniversity.edu", StudentId = "TU002", MajorId = 2, YearId = 4, LinkedInUrl = "https://linkedin.com/in/sarahwilliams", IsAdmin = false, CreatedAt = DateTime.UtcNow.AddDays(-40) },
                new Student { Id = 3, FirstName = "Michael", LastName = "Brown", Email = "michael.brown@techuniversity.edu", StudentId = "TU003", MajorId = 3, YearId = 5, LinkedInUrl = "https://linkedin.com/in/michaelbrown", IsAdmin = true, CreatedAt = DateTime.UtcNow.AddDays(-35) },
                new Student { Id = 4, FirstName = "Emily", LastName = "Davis", Email = "emily.davis@techuniversity.edu", StudentId = "TU004", MajorId = 4, YearId = 2, LinkedInUrl = "https://linkedin.com/in/emilydavis", CreatedAt = DateTime.UtcNow.AddDays(-30) },
                new Student { Id = 5, FirstName = "David", LastName = "Miller", Email = "david.miller@techuniversity.edu", StudentId = "TU005", MajorId = 1, YearId = 1, LinkedInUrl = "https://linkedin.com/in/davidmiller", CreatedAt = DateTime.UtcNow.AddDays(-25) }
            );
        });

        // Configure Project entity
        modelBuilder.Entity<Project>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.ExtendedDescription).HasColumnType("TEXT");
            entity.Property(e => e.SystemDesign).HasColumnType("TEXT");
            entity.Property(e => e.SystemDesignFormatted).HasColumnName("SystemDesignFormatted").HasMaxLength(2000);
            entity.Property(e => e.Priority).HasMaxLength(50);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.IsAvailable).HasDefaultValue(true);
            entity.Property(e => e.Kickoff).HasColumnName("Kickoff").HasDefaultValue(false);
            entity.Property(e => e.CriteriaIds).HasColumnName("CriteriaIds").HasMaxLength(500);

            // Foreign key relationships
            entity.HasOne(e => e.Organization)
                  .WithMany(o => o.Projects)
                  .HasForeignKey(e => e.OrganizationId)
                  .OnDelete(DeleteBehavior.SetNull);


            // Seed test data
            entity.HasData(
                new Project { Id = 1, Title = "Student Management System", Description = "Web application for managing student records and academic progress", Priority = "High", OrganizationId = 1, IsAvailable = true, CreatedAt = DateTime.UtcNow.AddDays(-30) },
                new Project { Id = 2, Title = "AI Research Platform", Description = "Machine learning platform for academic research", Priority = "Medium", OrganizationId = 2, IsAvailable = true, CreatedAt = DateTime.UtcNow.AddDays(-20) },
                new Project { Id = 3, Title = "Community Outreach App", Description = "Mobile app connecting volunteers with local community needs", Priority = "High", OrganizationId = 3, IsAvailable = true, CreatedAt = DateTime.UtcNow.AddDays(-90) },
                new Project { Id = 4, Title = "Online Learning Platform", Description = "E-learning platform with video streaming and assessments", Priority = "Critical", OrganizationId = 1, IsAvailable = true, CreatedAt = DateTime.UtcNow.AddDays(-15) },
                new Project { Id = 5, Title = "Data Analytics Dashboard", Description = "Real-time dashboard for analyzing student performance metrics", Priority = "Medium", OrganizationId = 1, IsAvailable = true, CreatedAt = DateTime.UtcNow.AddDays(-10) },
                new Project { Id = 6, Title = "Mobile App for Campus Events", Description = "Mobile application for students to discover and register for campus events", Priority = "Medium", OrganizationId = 1, IsAvailable = true, CreatedAt = DateTime.UtcNow.AddDays(-5) },
                new Project { Id = 7, Title = "Virtual Reality Learning Lab", Description = "VR environment for immersive learning experiences", Priority = "High", OrganizationId = 2, IsAvailable = true, CreatedAt = DateTime.UtcNow.AddDays(-3) },
                new Project { Id = 8, Title = "Blockchain Voting System", Description = "Secure voting system using blockchain technology", Priority = "High", OrganizationId = 1, IsAvailable = true, CreatedAt = DateTime.UtcNow.AddDays(-1) },
                new Project { Id = 9, Title = "IoT Smart Campus", Description = "Internet of Things system for campus management", Priority = "Medium", OrganizationId = 2, IsAvailable = true, CreatedAt = DateTime.UtcNow.AddDays(-2) }
            );
        });

        // Configure Role entity
        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.Category).HasMaxLength(50);
            entity.Property(e => e.Type).IsRequired().HasDefaultValue(0);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            // Note: Seed data removed from HasData() to prevent overwriting production data
            // Roles are seeded via migration (20250127000000_EnsureRolesDataWithoutOverwrite) 
            // that syncs prod with dev data:
            // 1. Product Manager (Leadership, Type 0)
            // 2. Frontend Developer (Technical, Type 2)
            // 3. Backend Developer (Technical, Type 2)
            // 4. UI/UX Designer (Technical, Type 3)
            // 5. Quality Assurance (Technical, Type 0, IsActive: false)
            // 6. Full Stack Developer (Leadership, Type 1)
            // 7. Marketing (Academic, Type 0)
            // 8. Documentation Specialist (Administrative, Type 0, IsActive: false)
        });

        // Configure StudentRole entity (many-to-many relationship)
        modelBuilder.Entity<StudentRole>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Notes).HasMaxLength(200);
            entity.Property(e => e.AssignedDate).HasDefaultValueSql("CURRENT_TIMESTAMP");

            // Foreign key relationships
            entity.HasOne(e => e.Student)
                  .WithMany(s => s.StudentRoles)
                  .HasForeignKey(e => e.StudentId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Role)
                  .WithMany(r => r.StudentRoles)
                  .HasForeignKey(e => e.RoleId)
                  .OnDelete(DeleteBehavior.Cascade);

            // Seed test data
            entity.HasData(
                new StudentRole { Id = 1, StudentId = 1, RoleId = 1, AssignedDate = DateTime.UtcNow.AddDays(-30), Notes = "Leading the Student Management System project", IsActive = true },
                new StudentRole { Id = 2, StudentId = 2, RoleId = 2, AssignedDate = DateTime.UtcNow.AddDays(-25), Notes = "Frontend development for multiple projects", IsActive = true },
                new StudentRole { Id = 3, StudentId = 3, RoleId = 3, AssignedDate = DateTime.UtcNow.AddDays(-20), Notes = "Backend development and database design", IsActive = true },
                new StudentRole { Id = 4, StudentId = 4, RoleId = 4, AssignedDate = DateTime.UtcNow.AddDays(-15), Notes = "UI/UX design for community outreach app", IsActive = true },
                new StudentRole { Id = 5, StudentId = 5, RoleId = 5, AssignedDate = DateTime.UtcNow.AddDays(-10), Notes = "QA testing for online learning platform", IsActive = true },
                new StudentRole { Id = 6, StudentId = 1, RoleId = 6, AssignedDate = DateTime.UtcNow.AddDays(-35), Notes = "Team lead for junior developers", IsActive = true },
                new StudentRole { Id = 7, StudentId = 3, RoleId = 7, AssignedDate = DateTime.UtcNow.AddDays(-18), Notes = "Research on AI and machine learning", IsActive = true },
                new StudentRole { Id = 8, StudentId = 2, RoleId = 8, AssignedDate = DateTime.UtcNow.AddDays(-12), Notes = "Documentation for frontend components", IsActive = true }
            );
        });

        // Configure JoinRequest entity
        modelBuilder.Entity<JoinRequest>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ChannelName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.ChannelId).IsRequired().HasMaxLength(50);
            entity.Property(e => e.StudentEmail).IsRequired().HasMaxLength(255);
            entity.Property(e => e.StudentFirstName).HasMaxLength(100);
            entity.Property(e => e.StudentLastName).HasMaxLength(100);
            entity.Property(e => e.ProjectTitle).HasMaxLength(200);
            entity.Property(e => e.Notes).HasMaxLength(500);
            entity.Property(e => e.ErrorMessage).HasMaxLength(1000);
            entity.Property(e => e.JoinDate).HasDefaultValueSql("CURRENT_TIMESTAMP");

            // Foreign key relationships
            entity.HasOne(e => e.Student)
                  .WithMany()
                  .HasForeignKey(e => e.StudentId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Project)
                  .WithMany()
                  .HasForeignKey(e => e.ProjectId)
                  .OnDelete(DeleteBehavior.Cascade);

            // Indexes for better performance
            entity.HasIndex(e => e.StudentEmail);
            entity.HasIndex(e => e.ChannelId);
            entity.HasIndex(e => e.Added);
            entity.HasIndex(e => e.JoinDate);
        });

        // Configure DesignVersion entity
        modelBuilder.Entity<DesignVersion>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.DesignDocument).IsRequired().HasColumnType("TEXT");
            entity.Property(e => e.CreatedBy).HasMaxLength(255);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            // Foreign key relationships
            entity.HasOne(e => e.Project)
                  .WithMany(p => p.DesignVersions)
                  .HasForeignKey(e => e.ProjectId)
                  .OnDelete(DeleteBehavior.Cascade);

            // Indexes for better performance
            entity.HasIndex(e => e.ProjectId);
            entity.HasIndex(e => e.VersionNumber);
            entity.HasIndex(e => e.IsActive);
        });

        // Configure ProjectBoard entity
        modelBuilder.Entity<ProjectBoard>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(50);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            
            // Explicit column mappings
            entity.Property(e => e.Id).HasColumnName("BoardId");
            entity.Property(e => e.ProjectId).HasColumnName("ProjectId");
            entity.Property(e => e.StatusId).HasColumnName("StatusId");
            entity.Property(e => e.AdminId).HasColumnName("AdminId");
            entity.Property(e => e.BoardUrl).HasColumnName("BoardURL").HasMaxLength(500);
            entity.Property(e => e.PublishUrl).HasColumnName("PublishUrl").HasMaxLength(500);
            entity.Property(e => e.MovieUrl).HasColumnName("MovieUrl").HasMaxLength(500);
            entity.Property(e => e.NextMeetingTime).HasColumnName("NextMeetingTime").HasColumnType("timestamp with time zone");
            entity.Property(e => e.NextMeetingUrl).HasColumnName("NextMeetingUrl").HasMaxLength(1000);
            entity.Property(e => e.GithubUrl).HasColumnName("GithubUrl").HasMaxLength(1000);
            entity.Property(e => e.GroupChat).HasColumnName("GroupChat").HasColumnType("text");
            entity.Property(e => e.Observed).HasColumnName("Observed").HasDefaultValue(0);
            
            // Foreign key relationships
            entity.HasOne(e => e.Project)
                  .WithMany()
                  .HasForeignKey(e => e.ProjectId)
                  .OnDelete(DeleteBehavior.Cascade);

            // Temporarily disable Status foreign key relationship
            // entity.HasOne(e => e.Status)
            //       .WithMany()
            //       .HasForeignKey(e => e.StatusId)
            //       .HasConstraintName("FK_ProjectBoards_ProjectStatuses")
            //       .OnDelete(DeleteBehavior.SetNull);

            // Temporarily disable Admin foreign key relationship
            // entity.HasOne(e => e.Admin)
            //       .WithMany()
            //       .HasForeignKey(e => e.AdminId)
            //       .OnDelete(DeleteBehavior.NoAction);

            // Indexes for better performance
            entity.HasIndex(e => e.ProjectId);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => e.StatusId);
        });

        // Configure BoardMeeting entity
        modelBuilder.Entity<BoardMeeting>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.BoardId).HasMaxLength(50).IsRequired();
            entity.Property(e => e.MeetingTime).IsRequired().HasColumnType("timestamp with time zone");
            entity.Property(e => e.StudentEmail).HasMaxLength(255);
            entity.Property(e => e.CustomMeetingUrl).HasColumnType("TEXT");
            entity.Property(e => e.ActualMeetingUrl).HasColumnType("TEXT");
            entity.Property(e => e.Attended).HasDefaultValue(false);
            entity.Property(e => e.JoinTime).HasColumnType("timestamp with time zone");
            
            // Foreign key relationship to ProjectBoard
            entity.HasOne(e => e.ProjectBoard)
                  .WithMany()
                  .HasForeignKey(e => e.BoardId)
                  .OnDelete(DeleteBehavior.Cascade);
            
            // Indexes for better performance
            entity.HasIndex(e => e.BoardId);
            entity.HasIndex(e => e.MeetingTime);
            entity.HasIndex(e => e.StudentEmail);
            entity.HasIndex(e => e.Attended);
        });

        // Configure ModuleType entity
        modelBuilder.Entity<ModuleType>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);

            // Seed test data
            entity.HasData(
                new ModuleType { Id = 1, Name = "Frontend" },
                new ModuleType { Id = 2, Name = "Backend" },
                new ModuleType { Id = 3, Name = "Database" },
                new ModuleType { Id = 4, Name = "Authentication" },
                new ModuleType { Id = 5, Name = "API" },
                new ModuleType { Id = 6, Name = "Mobile" },
                new ModuleType { Id = 7, Name = "DevOps" },
                new ModuleType { Id = 8, Name = "Testing" }
            );
        });

        // Configure ProjectModule entity
        modelBuilder.Entity<ProjectModule>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).HasMaxLength(100);
            entity.Property(e => e.Description).HasColumnType("text");
            entity.Property(e => e.Sequence).HasColumnName("Sequence");

            // Foreign key relationships
            entity.HasOne(e => e.Project)
                  .WithMany()
                  .HasForeignKey(e => e.ProjectId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.ModuleTypeNavigation)
                  .WithMany(mt => mt.ProjectModules)
                  .HasForeignKey(e => e.ModuleType)
                  .OnDelete(DeleteBehavior.Cascade);

            // Indexes for better performance
            entity.HasIndex(e => e.ProjectId);
            entity.HasIndex(e => e.ModuleType);
            entity.HasIndex(e => e.Sequence);
        });

        // Configure Figma entity
        modelBuilder.Entity<Figma>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.BoardId).IsRequired().HasMaxLength(50);
            entity.Property(e => e.FigmaAccessToken).HasMaxLength(512);
            entity.Property(e => e.FigmaRefreshToken).HasMaxLength(512);
            entity.Property(e => e.FigmaUserId).HasMaxLength(64);
            entity.Property(e => e.FigmaFileUrl).HasMaxLength(1024);
            entity.Property(e => e.FigmaFileKey).HasMaxLength(64);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("NOW()");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("NOW()");

            // Foreign key relationship
            entity.HasOne(e => e.ProjectBoard)
                  .WithMany()
                  .HasForeignKey(e => e.BoardId)
                  .HasPrincipalKey(pb => pb.Id)
                  .OnDelete(DeleteBehavior.Cascade);

            // Indexes for better performance
            entity.HasIndex(e => e.BoardId);
            entity.HasIndex(e => e.FigmaFileKey).IsUnique();
        });

        // Configure ProgrammingLanguage entity
        modelBuilder.Entity<ProgrammingLanguage>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Creator).HasMaxLength(200);
            entity.Property(e => e.Description).HasColumnType("TEXT");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            
            entity.HasIndex(e => e.Name).IsUnique();
            entity.HasIndex(e => e.IsActive);
        });

        // Configure ProjectsIDE entity
        modelBuilder.Entity<ProjectsIDE>(entity =>
        {
            entity.ToTable("ProjectsIDE");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ChunkId).IsRequired().HasMaxLength(100);
            entity.Property(e => e.ChunkType).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Status).HasMaxLength(50).HasDefaultValue("pending");
            entity.Property(e => e.FilesJson).HasColumnType("jsonb");
            entity.Property(e => e.FilesCount).HasDefaultValue(0);
            entity.Property(e => e.Dependencies).HasColumnType("text[]");
            entity.Property(e => e.ErrorMessage).HasColumnType("text");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            
            entity.HasOne(e => e.Project)
                  .WithMany(p => p.IDEChunks)
                  .HasForeignKey(e => e.ProjectId)
                  .OnDelete(DeleteBehavior.Cascade);
            
            entity.HasIndex(e => e.ProjectId);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.GenerationOrder);
            entity.HasIndex(e => new { e.ProjectId, e.ChunkId }).IsUnique();
        });

        // Configure Subscription entity
        modelBuilder.Entity<Subscription>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Description).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Price).IsRequired().HasColumnType("decimal(18,2)");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            
            // Seed data
            entity.HasData(
                new Subscription { Id = 1, Description = "Junior", Price = 0, CreatedAt = DateTime.UtcNow },
                new Subscription { Id = 2, Description = "Product", Price = 0, CreatedAt = DateTime.UtcNow },
                new Subscription { Id = 3, Description = "Enterprise A", Price = 0, CreatedAt = DateTime.UtcNow },
                new Subscription { Id = 4, Description = "Enterprise B", Price = 0, CreatedAt = DateTime.UtcNow }
            );
        });

        // Configure Employer entity
        modelBuilder.Entity<Employer>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Logo).HasColumnType("text");
            entity.Property(e => e.Website).HasMaxLength(500);
            entity.Property(e => e.ContactEmail).IsRequired().HasMaxLength(255);
            entity.Property(e => e.Phone).HasMaxLength(20);
            entity.Property(e => e.Address).HasMaxLength(500);
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.PasswordHash).HasMaxLength(256);
            entity.Property(e => e.SubscriptionTypeId).IsRequired();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            
            entity.HasOne(e => e.SubscriptionType)
                  .WithMany(s => s.Employers)
                  .HasForeignKey(e => e.SubscriptionTypeId)
                  .OnDelete(DeleteBehavior.Restrict);
            
            entity.HasIndex(e => e.ContactEmail);
            entity.HasIndex(e => e.SubscriptionTypeId);
        });

        // Configure EmployerBoard entity
        modelBuilder.Entity<EmployerBoard>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.EmployerId).IsRequired();
            entity.Property(e => e.BoardId).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Observed).HasDefaultValue(false);
            entity.Property(e => e.Approved).HasDefaultValue(false);
            entity.Property(e => e.Message).HasColumnType("text");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            
            entity.HasOne(e => e.Employer)
                  .WithMany(emp => emp.EmployerBoards)
                  .HasForeignKey(e => e.EmployerId)
                  .OnDelete(DeleteBehavior.Cascade);
            
            entity.HasOne(e => e.ProjectBoard)
                  .WithMany()
                  .HasForeignKey(e => e.BoardId)
                  .HasPrincipalKey(pb => pb.Id)
                  .OnDelete(DeleteBehavior.Cascade);
            
            entity.HasIndex(e => e.EmployerId);
            entity.HasIndex(e => e.BoardId);
            entity.HasIndex(e => new { e.EmployerId, e.BoardId }).IsUnique();
        });

        // Configure EmployerAdd entity
        modelBuilder.Entity<EmployerAdd>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.EmployerId).IsRequired();
            entity.Property(e => e.RoleId).IsRequired();
            entity.Property(e => e.Tags).HasColumnType("text");
            entity.Property(e => e.JobDescription).HasColumnType("text");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            
            entity.HasOne(e => e.Employer)
                  .WithMany(emp => emp.EmployerAdds)
                  .HasForeignKey(e => e.EmployerId)
                  .OnDelete(DeleteBehavior.Cascade);
            
            entity.HasOne(e => e.Role)
                  .WithMany()
                  .HasForeignKey(e => e.RoleId)
                  .OnDelete(DeleteBehavior.Restrict);
            
            entity.HasIndex(e => e.EmployerId);
            entity.HasIndex(e => e.RoleId);
        });

        // Configure EmployerCandidate entity
        modelBuilder.Entity<EmployerCandidate>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.EmployerId).IsRequired();
            entity.Property(e => e.StudentId).IsRequired();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            
            // Unique constraint to prevent duplicate employer-student pairs
            entity.HasIndex(e => new { e.EmployerId, e.StudentId }).IsUnique();
            
            entity.HasOne(e => e.Employer)
                  .WithMany(emp => emp.EmployerCandidates)
                  .HasForeignKey(e => e.EmployerId)
                  .OnDelete(DeleteBehavior.Cascade);
            
            entity.HasOne(e => e.Student)
                  .WithMany()
                  .HasForeignKey(e => e.StudentId)
                  .OnDelete(DeleteBehavior.Cascade);
            
            // Indexes for query performance
            entity.HasIndex(e => e.EmployerId);
            entity.HasIndex(e => e.StudentId);
            entity.HasIndex(e => e.CreatedAt);
        });

        // Update Student entity configuration to include new fields
        modelBuilder.Entity<Student>(entity =>
        {
            entity.Property(e => e.CV).HasColumnType("text");
            entity.Property(e => e.SubscriptionTypeId);
            entity.Property(e => e.MinutesToWork);
            entity.Property(e => e.HybridWork).HasDefaultValue(false);
            entity.Property(e => e.HomeWork).HasDefaultValue(false);
            entity.Property(e => e.FullTimeWork).HasDefaultValue(false);
            entity.Property(e => e.PartTimeWork).HasDefaultValue(false);
            entity.Property(e => e.FreelanceWork).HasDefaultValue(false);
            entity.Property(e => e.TravelWork).HasDefaultValue(false);
            entity.Property(e => e.NightShiftWork).HasDefaultValue(false);
            entity.Property(e => e.RelocationWork).HasDefaultValue(false);
            entity.Property(e => e.StudentWork).HasDefaultValue(false);
            entity.Property(e => e.MultilingualWork).HasDefaultValue(false);
            
            entity.HasOne(e => e.SubscriptionType)
                  .WithMany(s => s.Students)
                  .HasForeignKey(e => e.SubscriptionTypeId)
                  .OnDelete(DeleteBehavior.SetNull);
            
            entity.HasIndex(e => e.SubscriptionTypeId);
        });

        // Configure AIModel entity
        modelBuilder.Entity<AIModel>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.ToTable("AIModels");
            
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Provider).IsRequired().HasMaxLength(50);
            entity.Property(e => e.BaseUrl).HasMaxLength(500);
            entity.Property(e => e.ApiVersion).HasMaxLength(50);
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.MaxTokens).HasColumnType("integer");
            entity.Property(e => e.DefaultTemperature).HasColumnType("double precision");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.UpdatedAt).HasColumnType("timestamp with time zone");

            // Seed data for existing models
            entity.HasData(
                new AIModel
                {
                    Id = 1,
                    Name = "gpt-4o-mini",
                    Provider = "OpenAI",
                    BaseUrl = "https://api.openai.com/v1",
                    ApiVersion = null,
                    MaxTokens = 16384,
                    DefaultTemperature = 0.2,
                    Description = "OpenAI GPT-4o Mini model - fast and cost-effective",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                },
                new AIModel
                {
                    Id = 2,
                    Name = "claude-sonnet-4-5-20250929",
                    Provider = "Anthropic",
                    BaseUrl = "https://api.anthropic.com/v1",
                    ApiVersion = "2023-06-01",
                    MaxTokens = 200000,
                    DefaultTemperature = 0.3,
                    Description = "Anthropic Claude Sonnet 4.5 model - powerful for complex tasks",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                }
            );
        });
    }
}
