﻿namespace Showcase.Server.Api.Controllers
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Web.Http;
    using System.Web.OData.Query;

    using AutoMapper.QueryableExtensions;

    using Showcase.Data.Common.Repositories;
    using Showcase.Data.Models;
    using Showcase.Server.Api.Infrastructure.Extensions;
    using Showcase.Server.Api.Infrastructure.FileSystem;
    using Showcase.Server.Api.Infrastructure.Validation;
    using Showcase.Server.Common;
    using Showcase.Server.DataTransferModels;
    using Showcase.Server.DataTransferModels.Project;
    using Showcase.Services.Common.Extensions;
    using Showcase.Services.Data.Contracts;
    using Showcase.Services.Data.Models;
    using Showcase.Services.Logic.Contracts;

    [RoutePrefix("api/Projects")]
    public class ProjectsController : ApiController
    {
        private readonly ILikesService likesService;
        private readonly IVisitsService visitsService;
        private readonly IProjectsService projectsService;
        private readonly ITagsService tagsService;
        private readonly IUsersService usersService;
        private readonly IMappingService mappingService;
        private readonly IImagesService imagesService;
        private readonly IFileSystemService fileSystemService;

        private readonly IFlagsService flagsService;

        public ProjectsController(
            ILikesService likesService,
            IVisitsService visitsService,
            IProjectsService projectsService,
            IUsersService usersService,
            IFlagsService flagsService,
            ITagsService tagsService,
            IMappingService mappingService,
            IImagesService imagesService,
            IFileSystemService fileSystemService)
        {
            this.likesService = likesService;
            this.visitsService = visitsService;
            this.projectsService = projectsService;
            this.tagsService = tagsService;
            this.usersService = usersService;
            this.flagsService = flagsService;
            this.mappingService = mappingService;
            this.imagesService = imagesService;
            this.fileSystemService = fileSystemService;
        }

        [HttpGet]
        public IHttpActionResult Get()
        {
            var model = this.projectsService
                .LatestProjects()
                .Project()
                .To<ProjectSimpleResponseModel>()
                .ToList();

            return this.Data(model);
        }

        [Authorize]
        [HttpPost]
        [ValidateModel]
        public async Task<IHttpActionResult> Post(ProjectRequestModel project)
        {
            var collaborators = await this.usersService.CollaboratorsFromCommaSeparatedValues(project.Collaborators);
            var tags = await this.tagsService.TagsFromCommaSeparatedValues(project.Tags);
            var processedImages = await this.imagesService.ProcessImages(project.Images.Select(FileRequestModel.ToRawImage));
            await this.fileSystemService.SaveImagesToFiles(processedImages);

            var addedProject = await this.projectsService.AddNew(
                this.mappingService.Map<Project>(project),
                collaborators, 
                tags, 
                processedImages,
                project.MainImage);

            return this.Ok(this.mappingService.Map<PostProjectResponseModel>(addedProject));
        }

        [HttpGet]
        [Route("Popular")]
        public IHttpActionResult Popular()
        {
            var model = this.projectsService
                .MostPopular()
                .Project()
                .To<ProjectSimpleResponseModel>()
                .ToList();

            return this.Data(model);
        }

        [HttpGet]
        public IHttpActionResult Get(int id)
        {
            var username = this.User.Identity.Name;

            var model = this.projectsService
                .ProjectById(id)
                .Project()
                .To<ProjectResponseModel>()
                .FirstOrDefault();

            model.IsLiked = this.likesService.ProjectIsLikedByUser(id, username);
            model.IsFlagged = this.flagsService.ProjectIsFlaggedByUser(id, username);

            return this.Data(model);
        }

        [HttpGet]
        [Route("LikedProjects/{username}")]
        public IHttpActionResult LikedProjects(string username)
        {
            var currentLoggedInUsername = this.User.Identity.Name;
            if (username != currentLoggedInUsername.ToLower())
            {
                return this.Data(false, "You are not authorized to view this user's liked projects.");
            }

            var userId = this.usersService.UserIdByUsername(username);

            var model = this.projectsService
                .LikedByUser(userId)
                .Project()
                .To<ProjectResponseModel>()
                .ToList();

            return this.Data(model);
        }

        [HttpPost]
        [Route("Visit/{id}")]
        public IHttpActionResult Visit(int id)
        {
            var username = this.User.Identity.Name;

            this.visitsService.VisitProject(id, username);

            return this.Ok();
        }

        // [Authorize]
        [HttpPost]
        [Route("Like/{id}")]
        public IHttpActionResult Like(int id)
        {
            var username = this.User.Identity.Name;

            if (this.likesService.ProjectIsLikedByUser(id, username))
            {
                return this.Data(false, "You already have liked this project.");
            }

            this.likesService.LikeProject(id, username);

            return this.Ok();
        }

        // [Authorize]
        [HttpPost]
        [Route("Dislike/{id}")]
        public IHttpActionResult Dislike(int id)
        {
            var username = this.User.Identity.Name;

            if (!this.likesService.ProjectIsLikedByUser(id, username))
            {
                return this.Data(false, "You have not yet liked this project.");
            }

            this.likesService.DislikeProject(id, username);

            return this.Ok();
        }

        [HttpPost]
        [Route("Flag/{id}")]
        public IHttpActionResult Flag(int id)
        {
            var username = this.User.Identity.Name;

            if (this.flagsService.ProjectIsFlaggedByUser(id, username))
            {
                return this.Data(false, "You can't flagg the same project more than once.");
            }

            this.flagsService.FlagProject(id, username);

            return this.Ok();
        }

        [HttpPost]
        [Route("Unflag/{id}")]
        public IHttpActionResult Unflag(int id)
        {
            var username = this.User.Identity.Name;

            if (!this.flagsService.ProjectIsFlaggedByUser(id, username))
            {
                return this.Data(false, "You have not yet flagged this project.");
            }

            this.flagsService.UnFlagProject(id, username);

            return this.Ok();
        }

        [HttpGet]
        [Route("Search")]
        public ODataResult<ProjectSimpleResponseModel> Search(ODataQueryOptions<ProjectSimpleResponseModel> options)
        {
            options.Validate(new ODataValidationSettings()
            {
                MaxTop = 64,
                AllowedQueryOptions = AllowedQueryOptions.Filter | AllowedQueryOptions.OrderBy |
                    AllowedQueryOptions.Skip | AllowedQueryOptions.Top |
                    AllowedQueryOptions.Select | AllowedQueryOptions.Count
            });

            var projects = this.projectsService
                .QueriedProjects()
                .Project()
                .To<ProjectSimpleResponseModel>();

            long? count = null;
            if (options.Count != null && options.Count.Value)
            {
                count = projects.Count();
            }

            ODataQuerySettings settings = new ODataQuerySettings() { PageSize = options.Top != null ? options.Top.Value : 8 };
            projects = options.ApplyTo(projects, settings) as IQueryable<ProjectSimpleResponseModel>; // TODO: move to extension method of IQueryable

            return new ODataResult<ProjectSimpleResponseModel>(
                 projects as IEnumerable<ProjectSimpleResponseModel>, count); // TODO: move to extension method like this.Data
        }
    }
}