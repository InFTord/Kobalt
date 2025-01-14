﻿using Kobalt.Infractions.Data;
using Kobalt.Infractions.Infrastructure.Mediator.DTOs;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Kobalt.Infractions.Infrastructure.Mediator.Mediator;

// GET /infractions/guilds/{guildID}/users/{userID}
public record GetInfractionsForUserRequest(ulong GuildID, ulong UserID) : IRequest<IEnumerable<InfractionDTO>>;

public class GetInfractionsForUserHandler : IRequestHandler<GetInfractionsForUserRequest, IEnumerable<InfractionDTO>>
{
    private readonly InfractionContext _db;

    public GetInfractionsForUserHandler(InfractionContext db)
    {
        _db = db;
    }

    public async ValueTask<IEnumerable<InfractionDTO>> Handle(GetInfractionsForUserRequest request, CancellationToken cancellationToken)
    {
        var infractions = await _db.Infractions
            .Where(x => x.GuildID == request.GuildID && x.UserID == request.UserID)
            .ToListAsync(cancellationToken);

        return infractions.Select
        (
            x => new InfractionDTO
            (
                x.Id,
                x.ReferencedId,
                x.IsHidden,
                x.Reason,
                x.UserID,
                x.GuildID,
                x.ModeratorID,
                x.Type,
                x.CreatedAt,
                x.ExpiresAt
            )
        );
    }
}
