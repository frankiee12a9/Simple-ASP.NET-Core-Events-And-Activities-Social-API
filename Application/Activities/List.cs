using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application.Core;
using Application.Interfaces;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Domain;
using MediatR;
using Microsoft.Extensions.Logging;
using Persistence;

namespace Application.Activities
{
	public class List
	{
		public class Query : IRequest<Result<PagedList<ActivityDTO>>>
		{
			public ActivityParams Params { get; set; }
		}

		public class Handler : IRequestHandler<Query, Result<PagedList<ActivityDTO>>>
		{
			private readonly AppDbContext _context;
			private readonly ILogger<List> _logger;
			private readonly IMapper _mapper;
			private readonly IUserAccessor _userAccessor;

			public Handler(AppDbContext context, ILogger<List> logger, IMapper mapper, IUserAccessor userAccessor)
			{
				_context = context;
				_logger = logger;
				_mapper = mapper;
				_userAccessor = userAccessor;
			}

			public async Task<Result<PagedList<ActivityDTO>>> Handle(Query request, CancellationToken cancellationToken)
			{
				// using traditional related data loading => not optimized
				// var activities = await _context.Activities
				//  .Include(x => x.Attendees)
				//  .ThenInclude(y => y.Activity)
				//  .ToListAsync(cancellationToken);

				// using ProjectTo => optimized and short
				var query = _context.Activities
					.Where(x => x.Date >= request.Params.StartDate)
					.OrderBy(x => x.Date)
					.ProjectTo<ActivityDTO>(_mapper.ConfigurationProvider,
						new { currentUsername = _userAccessor.GetUserName() })
					.AsQueryable();

				// isGoing only filter
				if (request.Params.IsGoing && !request.Params.IsHosting)
				{
					var newQuery = query.Where(x => x.Attendees.Any(y => y.Username == _userAccessor.GetUserName())
						&& x.HostUsername != _userAccessor.GetUserName());
					query = newQuery;
				}

				// isHosting only filter
				if (request.Params.IsHosting && !request.Params.IsGoing)
				{
					query = query.Where(x => x.HostUsername == _userAccessor.GetUserName());
				}

				return Result<PagedList<ActivityDTO>>.Success(
					await PagedList<ActivityDTO>.CreateAsync(query, request.Params.PageNumber,
						request.Params.PageSize)
				);
			}
		}
	}
}
