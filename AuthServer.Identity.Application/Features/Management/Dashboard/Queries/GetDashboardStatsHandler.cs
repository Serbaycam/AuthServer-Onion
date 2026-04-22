using AuthServer.Identity.Application.Dtos;
using AuthServer.Identity.Application.Interfaces;
using AuthServer.Identity.Application.Wrappers;
using AuthServer.Identity.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AuthServer.Identity.Application.Features.Management.Dashboard.Queries
{

    public class GetDashboardStatsHandler : IRequestHandler<GetDashboardStatsQuery, ServiceResponse<DashboardStatsDto>>
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly RoleManager<AppRole> _roleManager;
        private readonly IApplicationDbContext _context;

        public GetDashboardStatsHandler(UserManager<AppUser> userManager, RoleManager<AppRole> roleManager, IApplicationDbContext context)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _context = context;
        }

        public async Task<ServiceResponse<DashboardStatsDto>> Handle(GetDashboardStatsQuery request, CancellationToken cancellationToken)
        {
            // 1. Genel İstatistikler
            var totalUsers = await _userManager.Users.CountAsync(cancellationToken);
            var activeUsers = await _userManager.Users.CountAsync(u => u.IsActive, cancellationToken);

            var activeSessions = await _context.RefreshTokens
                .CountAsync(x => x.RevokedDate == null && x.Expires > DateTime.UtcNow, cancellationToken);

            // 2. Son Aktiviteleri Çek (Henüz Email yok, sadece UserId var)
            var lastLogs = await _context.AuditLogs
                .OrderByDescending(x => x.CreatedDate)
                .Take(10) // Son 10 hareketi görelim
                .ToListAsync(cancellationToken);

            // 3. İyileştirme: Loglardaki UserID'leri toplayıp E-Postalarını bulalım
            // Bu yöntem N+1 problemini engeller. Tek sorguda tüm gerekli mailleri çekeriz.
            var userIdsInLogs = lastLogs
                .Select(x => x.UserId)
                .Distinct()
                .ToList();

            // Veritabanındaki string ID ile Guid ID dönüşümüne dikkat edelim
            // (AuditLog.UserId string tutuluyor, AppUser.Id Guid olabilir)
            var userEmails = await _userManager.Users
                .Where(u => userIdsInLogs.Contains(u.Id.ToString()))
                .ToDictionaryAsync(u => u.Id.ToString(), u => u.Email, cancellationToken);

            // 4. Logları DTO'ya dönüştürürken E-Posta eşleşmesi yap
            var logDtos = lastLogs.Select(x => new AuditLogDto
            {
                Action = x.Action,
                Date = x.CreatedDate,
                IpAddress = x.IpAddress,
                // Sözlükte varsa maili yaz, yoksa (örn: silinmiş kullanıcı) ID'yi yaz
                UserEmail = userEmails.ContainsKey(x.UserId) ? userEmails[x.UserId] : x.UserId
            }).ToList();

            var totalRoles = await _roleManager.Roles.CountAsync(cancellationToken);

            var stats = new DashboardStatsDto
            {
                TotalUsers = totalUsers,
                ActiveUsers = activeUsers,
                PassiveUsers = totalUsers - activeUsers,
                TotalRoles = totalRoles,
                TotalActiveSessions = activeSessions,
                LatestActivities = logDtos
            };

            return new ServiceResponse<DashboardStatsDto>(stats);
        }
    }
}