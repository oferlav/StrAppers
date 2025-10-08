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
    public DbSet<Role> Roles { get; set; }
    public DbSet<StudentRole> StudentRoles { get; set; }
    public DbSet<Major> Majors { get; set; }
    public DbSet<Year> Years { get; set; }
    public DbSet<JoinRequest> JoinRequests { get; set; }
    public DbSet<DesignVersion> DesignVersions { get; set; }
    public DbSet<ProjectBoard> ProjectBoards { get; set; }

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

            // Foreign key relationship to ProjectBoard (Trello board)
            entity.HasOne(e => e.ProjectBoard)
                  .WithMany()
                  .HasForeignKey(e => e.BoardId)
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
            entity.Property(e => e.Priority).HasMaxLength(50);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.IsAvailable).HasDefaultValue(true);

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
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            // Seed test data
            entity.HasData(
                new Role { Id = 1, Name = "Project Manager", Description = "Leads project planning and execution", Category = "Leadership", IsActive = true, CreatedAt = DateTime.UtcNow.AddDays(-40) },
                new Role { Id = 2, Name = "Frontend Developer", Description = "Develops user interface and user experience", Category = "Technical", IsActive = true, CreatedAt = DateTime.UtcNow.AddDays(-40) },
                new Role { Id = 3, Name = "Backend Developer", Description = "Develops server-side logic and database integration", Category = "Technical", IsActive = true, CreatedAt = DateTime.UtcNow.AddDays(-40) },
                new Role { Id = 4, Name = "UI/UX Designer", Description = "Designs user interface and user experience", Category = "Technical", IsActive = true, CreatedAt = DateTime.UtcNow.AddDays(-40) },
                new Role { Id = 5, Name = "Quality Assurance", Description = "Tests software and ensures quality standards", Category = "Technical", IsActive = true, CreatedAt = DateTime.UtcNow.AddDays(-40) },
                new Role { Id = 6, Name = "Team Lead", Description = "Provides guidance and mentorship to team members", Category = "Leadership", IsActive = true, CreatedAt = DateTime.UtcNow.AddDays(-40) },
                new Role { Id = 7, Name = "Research Assistant", Description = "Conducts research and data analysis", Category = "Academic", IsActive = true, CreatedAt = DateTime.UtcNow.AddDays(-40) },
                new Role { Id = 8, Name = "Documentation Specialist", Description = "Creates and maintains project documentation", Category = "Administrative", IsActive = true, CreatedAt = DateTime.UtcNow.AddDays(-40) }
            );
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
    }
}
