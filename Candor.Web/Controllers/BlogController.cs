﻿using AutoMapper;
using Candor.Domain.Models;
using Candor.Infrastructure.Common.Exceptions;
using Candor.Infrastructure.Common.Utilities;
using Candor.UseCases.Authorization.GetCurrentUser;
using Candor.UseCases.Blog.Comments.CreateComment;
using Candor.UseCases.Blog.Comments.FindCommentById;
using Candor.UseCases.Blog.Posts.CreatePost;
using Candor.UseCases.Blog.Posts.DeletePost;
using Candor.UseCases.Blog.Posts.EditPost;
using Candor.UseCases.Blog.Posts.FindPostById;
using Candor.UseCases.Blog.Posts.GetAllPosts;
using Candor.Web.ViewModels.Blog;
using Candor.Web.Views.Components;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Candor.Web.Controllers;

/// <summary>
/// Blog controller.
/// </summary>
public class BlogController : Controller
{
    private const int PageSize = 10;

    private readonly IMediator mediator;
    private readonly IMapper mapper;

    /// <summary>
    /// Constructor.
    /// </summary>
    public BlogController(IMediator mediator, IMapper mapper)
    {
        (this.mediator, this.mapper) = (mediator, mapper); // Deconstructing assignment
    }

    /// <summary>
    /// Main page.
    /// </summary>
    /// <param name="pageNumber">Page number, used for pagination.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>View.</returns>
    public async Task<IActionResult> IndexAsync(int? pageNumber, CancellationToken cancellationToken)
    {
        var posts = await mediator.Send(new GetAllPostsQuery(), cancellationToken);

        posts = posts.Where(post => post.IsPublic).OrderByDescending(post => post.CreatedAt);

        var paginatedPosts = PaginatedList<Post>.Create(posts, pageNumber ?? 1, PageSize);

        return View(paginatedPosts);
    }

    /// <summary>
    /// GET: User's blog page.
    /// </summary>
    /// <returns>View.</returns>
    [HttpGet("/Blog")]
    [Authorize]
    public IActionResult UserBlog()
    {
        return View();
    }

    /// <summary>
    /// GET: Create post page.
    /// </summary>
    /// <returns>View.</returns>
    [Authorize]
    [HttpGet("/CreatePost")]
    public IActionResult CreatePost()
    {
        return View();
    }

    /// <summary>
    /// POST: Creates post.
    /// </summary>
    /// <param name="viewModel">Create post view model.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>Redirect to user's blog.</returns>
    [HttpPost("/CreatePost")]
    [Authorize]
    public async Task<IActionResult> CreatePostAsync(CreatePostViewModel viewModel, CancellationToken cancellationToken)
    {
        if (ModelState.IsValid)
        {
            var user = await mediator.Send(new GetCurrentUserQuery(), cancellationToken);

            var command = new CreatePostCommand
            {
                Title = viewModel.Title,
                Content = viewModel.Content,
                IsPublic = viewModel.IsPublic,
                UserId = user.Id
            };

            await mediator.Send(command, cancellationToken);

            return RedirectToAction("UserBlog");
        }

        return View();
    }

    /// <summary>
    /// GET: Post page.
    /// </summary>
    /// <param name="id">Post id.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>View.</returns>
    [HttpGet("/Post/{id:int}")]
    public async Task<IActionResult> PostAsync(int id, CancellationToken cancellationToken)
    {
        try
        {
            var post = await mediator.Send(new FindPostByIdQuery(id), cancellationToken);

            if (!Request.Cookies.ContainsKey($"Post{id}Visited"))
            {
                post.ViewsCount++;

                Response.Cookies.Append($"Post{id}Visited", "Visited", new CookieOptions
                {
                    Expires = DateTimeOffset.UtcNow.AddDays(30)
                });

                await mediator.Send(new EditPostCommand(post), cancellationToken);
            }
            return View(post);
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>
    /// GET: Edit post page.
    /// </summary>
    /// <param name="id">Post id.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>View.</returns>
    [HttpGet("/Post/{id:int}/Edit")]
    public async Task<IActionResult> EditPostAsync(int id, CancellationToken cancellationToken)
    {
        try
        {
            var post = await mediator.Send(new FindPostByIdQuery(id), cancellationToken);

            return View(post);
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>
    /// POST: Edit post.
    /// </summary>
    /// <param name="id">Post id.</param>
    /// <param name="viewModel">View model.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>Redirect to the post.</returns>
    [HttpPost("/Post/{id:int}/Edit")]
    public async Task<IActionResult> EditPostAsync(int id, [FromForm] EditPostViewModel viewModel, CancellationToken cancellationToken)
    {
        if (ModelState.IsValid)
        {
            var post = await mediator.Send(new FindPostByIdQuery(id), cancellationToken);

            var postMapped = mapper.Map(viewModel, post);

            await mediator.Send(new EditPostCommand(postMapped), cancellationToken);

            return LocalRedirect($"/Post/{id}");
        }

        return View();
    }

    /// <summary>
    /// POST: Delete post.
    /// </summary>
    /// <param name="id">Post id.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>Redirect to user's blog.</returns>
    [HttpPost("/Post/{id:int}")]
    public async Task<IActionResult> DeletePostAsync(int id, CancellationToken cancellationToken)
    {
        var post = await mediator.Send(new FindPostByIdQuery(id), cancellationToken);

        await mediator.Send(new DeletePostCommand(post), cancellationToken);

        return RedirectToAction("UserBlog");
    }

    /// <summary>
    /// PUT: Increments likes on the post.
    /// </summary>
    /// <param name="id">Post id.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>Content with likes.</returns>
    [HttpPut("/Like")]
    public async Task<IActionResult> LikeAsync(int id, CancellationToken cancellationToken)
    {
        var post = await mediator.Send(new FindPostByIdQuery(id), cancellationToken);

        if (Request.Cookies.ContainsKey($"Post{id}Liked"))
        {
            post.Likes--;

            Response.Cookies.Delete($"Post{id}Liked");
        }
        else
        {
            post.Likes++;

            Response.Cookies.Append($"Post{id}Liked", "Liked", new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddYears(1)
            });
        }

        await mediator.Send(new EditPostCommand(post), cancellationToken);

        return Content(post.Likes.ToString());
    }

    /// <summary>
    /// Creates comment.
    /// </summary>
    /// <param name="viewModel">Create comment view model.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>Redirect to post.</returns>
    public async Task<IActionResult> CreateComment(CreateCommentViewModel viewModel, CancellationToken cancellationToken)
    {
        var user = await mediator.Send(new GetCurrentUserQuery(), cancellationToken);

        var postId = viewModel.PostId;

        var post = await mediator.Send(new FindPostByIdQuery(postId), cancellationToken);

        var command = new CreateCommentCommand
        {
            User = user,
            Post = post,
            Content = viewModel.Content
        };

        if (viewModel.CommentId != default)
        {
            var comment = await mediator.Send(new FindCommentByIdQuery(viewModel.CommentId), cancellationToken);
            command = command with
            {
                CommentReply = comment
            };
        }

        await mediator.Send(command, cancellationToken);

        return LocalRedirect($"/Post/{postId}");
    }

    /// <summary>
    /// GET: Get user's posts.
    /// </summary>
    /// <param name="isPublic">Public privacy of the posts.</param>
    /// <param name="isPrivate">Private privacy of the posts.</param>
    /// <param name="pageNumber">Page number, used for pagination.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>Posts depends on privacy.</returns>
    [HttpGet("/Posts")]
    public async Task<IActionResult> GetPosts(bool isPublic, bool isPrivate, int? pageNumber, CancellationToken cancellationToken)
    {
        var user = await mediator.Send(new GetCurrentUserQuery(), cancellationToken);

        var posts = user.Posts.AsQueryable();

        if (!isPublic && !isPrivate)
        {
            posts = posts.Take(0);
        }
        else if (isPublic && !isPrivate)
        {
            posts = posts.Where(p => p.IsPublic == isPublic);
        }
        else if (!isPublic && isPrivate)
        {
            posts = posts.Where(p => p.IsPublic == !isPrivate);
        }

        posts = posts.OrderByDescending(post => post.CreatedAt);

        var paginatedPosts = PaginatedList<Post>.Create(posts, pageNumber ?? 1, PageSize);

        return ViewComponent(nameof(Blog), new { posts = paginatedPosts });
    }
}
