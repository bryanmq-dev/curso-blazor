using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using sharpness_sharp.Data;
using sharpness_sharp.Data.Entities;
using sharpness_sharp.Data.Enums;

namespace sharpness_sharp.Features.Reservations;

public sealed class ReservationService(
    ApplicationAuthDbContext db,
    UserManager<ApplicationUser> userManager)
{
    public async Task<ReservationStatusChangeResult> CreateAsync(
        string userId,
        int travelDepartureId,
        int numberOfPeople,
        string? observations = null,
        CancellationToken cancellationToken = default)
    {
        if (numberOfPeople <= 0)
            return ReservationStatusChangeResult.Failure("La cantidad de personas debe ser mayor a cero.");

        var user = await userManager.FindByIdAsync(userId);
        if (user is null || !await userManager.IsInRoleAsync(user, "Cliente"))
            return ReservationStatusChangeResult.Failure("Solo los clientes pueden crear reservas.");

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var departure = await db.TravelDepartures
            .Include(item => item.TravelPackage)
            .SingleOrDefaultAsync(item => item.Id == travelDepartureId, cancellationToken);

        if (departure is null || departure.TravelPackage is null || !departure.IsActive || !departure.TravelPackage.IsActive)
            return ReservationStatusChangeResult.Failure("La salida seleccionada no esta disponible.");

        if (departure.DepartureDate <= DateTime.UtcNow || departure.AvailableSeats < numberOfPeople)
            return ReservationStatusChangeResult.Failure("La salida no tiene plazas suficientes o ya paso.");

        var reservation = new Reservation
        {
            Code = $"RES-{Guid.NewGuid():N}"[..16].ToUpperInvariant(),
            UserId = userId,
            TravelDepartureId = departure.Id,
            NumberOfPeople = numberOfPeople,
            UnitPrice = departure.Price,
            TotalPrice = departure.Price * numberOfPeople,
            Observations = observations,
            Status = ReservationStatus.Pendiente,
            CreatedAt = DateTime.UtcNow,
        };

        departure.ReservedSeats += numberOfPeople;
        reservation.StatusHistory.Add(new ReservationStatusHistory
        {
            PreviousStatus = string.Empty,
            NewStatus = ReservationStatus.Pendiente.ToString(),
            ChangedAt = reservation.CreatedAt,
            ChangedByUserId = userId,
        });

        db.Reservations.Add(reservation);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ReservationStatusChangeResult.Success(reservation);
    }

    public Task<List<Reservation>> GetForUserAsync(string userId, CancellationToken cancellationToken = default) =>
        db.Reservations
            .AsNoTracking()
            .Where(item => item.UserId == userId)
            .Include(item => item.TravelDeparture!)
                .ThenInclude(departure => departure.TravelPackage!)
                    .ThenInclude(package => package.Destination)
            .Include(item => item.StatusHistory)
                .ThenInclude(history => history.ChangedByUser)
            .OrderByDescending(item => item.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task<ReservationStatusChangeResult> ChangeStatusAsync(
        int reservationId,
        ReservationStatus newStatus,
        string actingUserId,
        CancellationToken cancellationToken = default)
    {
        var actingUser = await userManager.FindByIdAsync(actingUserId);
        if (actingUser is null)
            return ReservationStatusChangeResult.Failure("No se encontro el usuario autenticado.");

        var reservation = await db.Reservations
            .Include(item => item.StatusHistory)
            .SingleOrDefaultAsync(item => item.Id == reservationId, cancellationToken);

        if (reservation is null)
            return ReservationStatusChangeResult.Failure("La reserva no existe.");

        var isEmployee = await userManager.IsInRoleAsync(actingUser, "Empleado") ||
                         await userManager.IsInRoleAsync(actingUser, "Admin");
        if (!isEmployee && reservation.UserId != actingUserId)
            return ReservationStatusChangeResult.Failure("No tienes permiso para modificar esta reserva.");

        if (!isEmployee && newStatus != ReservationStatus.Cancelada)
            return ReservationStatusChangeResult.Failure("Solo puedes cancelar tus propias reservas.");

        if (!IsAllowedTransition(reservation.Status, newStatus))
            return ReservationStatusChangeResult.Failure(
                $"No se puede cambiar de {reservation.Status} a {newStatus}.");

        if (reservation.Status == newStatus)
            return ReservationStatusChangeResult.Success(reservation);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var previousStatus = reservation.Status;
        reservation.Status = newStatus;
        reservation.StatusHistory.Add(new ReservationStatusHistory
        {
            ReservationId = reservation.Id,
            PreviousStatus = previousStatus.ToString(),
            NewStatus = newStatus.ToString(),
            ChangedAt = DateTime.UtcNow,
            ChangedByUserId = actingUserId
        });

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ReservationStatusChangeResult.Success(reservation);
    }

    private static bool IsAllowedTransition(ReservationStatus current, ReservationStatus next) =>
        current switch
        {
            ReservationStatus.Pendiente => next is ReservationStatus.Confirmada or ReservationStatus.Rechazada or ReservationStatus.Cancelada,
            ReservationStatus.Confirmada => next is ReservationStatus.Completada or ReservationStatus.Cancelada,
            ReservationStatus.Rechazada => false,
            ReservationStatus.Cancelada => false,
            ReservationStatus.Completada => false,
            _ => false
        };
}

public sealed record ReservationStatusChangeResult(bool Succeeded, string? Error, Reservation? Reservation)
{
    public static ReservationStatusChangeResult Failure(string error) => new(false, error, null);
    public static ReservationStatusChangeResult Success(Reservation reservation) => new(true, null, reservation);
}
