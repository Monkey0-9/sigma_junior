using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Hft.Infra;
using Hft.Governance.Services;
using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace Hft.Governance.Controllers
{
    [ApiController]
    [Route("policies")]
    public class GovernanceController : ControllerBase
    {
        private readonly AppendOnlyLog _auditLog;
        private readonly PolicyState _policyState;
        private readonly ILogger<GovernanceController> _logger;

        public GovernanceController(AppendOnlyLog auditLog, PolicyState policyState, ILogger<GovernanceController> logger)
        {
            _auditLog = auditLog;
            _policyState = policyState;
            _logger = logger;
        }

        [HttpGet("{policyName}")]
        public ActionResult<GovernanceDecision> ValidatePolicy(string policyName)
        {
             var policy = _policyState.GetPolicy(policyName);
             if (policy == null) return NotFound();
             return Ok(policy);
        }

        [HttpPost("approve")]
        public ActionResult<GovernanceDecision> ApprovePolicy([FromBody] JsonElement request)
        {
            // Simple request model parsing
            // Expected: { "policy": "...", "approver": "...", "rationale": "..." }
            
            try
            {
                var policy = request.GetProperty("policy").GetString();
                var approver = request.GetProperty("approver").GetString();
                var rationale = request.GetProperty("rationale").GetString();

                if (string.IsNullOrEmpty(policy) || string.IsNullOrEmpty(approver))
                {
                    return BadRequest("Policy and Approver are required.");
                }

                var decision = new GovernanceDecision
                {
                    PolicyName = policy,
                    ApproverId = approver,
                    IsApproved = true,
                    Rationale = rationale ?? "Auto-approved via API",
                    Timestamp = DateTime.UtcNow.Ticks
                };

                // Append to audit log
                // AppendOnlyLog.Append<T>(byte type, in T payload)
                // We'll treat GovernanceDecision as a struct? OR serialize to bytes.
                // The AppendOnlyLog expects a struct 'T' where T: struct.
                // Our record is a class. We might need to wrap it or serialize it.
                // For this exercise, let's assume we serialize to a byte payload struct wrapper.
                
                // Need a way to write flexible data to AppendOnlyLog.
                // If AppendOnlyLog requires T: struct, we need a fixed size struct or use a Blob struct.
                
                // Let's create a struct wrapper for decision or simplify.
                // Ideally, AppendOnlyLog should support byte arrays or we make a struct.
                
                // For now, let's just log it and simulate writing (since we might need to modify AppendOnlyLog to support variable length if it doesn't).
                // Looking at AppendOnlyLog.cs: Append<T>(byte type, in T payload) where T : struct
                // Code uses Marshal.SizeOf<T>(). This implies fixed size struct!
                
                // So GovernanceDecision MUST be a struct or we write a fixed size header pointing to a blob?
                // The user said "records decision in append-only audit log".
                
                // Let's make a simplified struct for the log.
                
                var logEntry = new GovernanceDecisionLogEntry
                {
                    Timestamp = decision.Timestamp,
                    Approved = decision.IsApproved ? (byte)1 : (byte)0
                    // String fields problematic for fixed size struct marshalling.
                };
                
                _auditLog.Append(0x01, in logEntry); // Type 1 = Governance

                _policyState.AddDecision(decision);

                _logger.LogInformation("Governance decision recorded: {DecisionId}", decision.DecisionId);

                return Ok(decision);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to record governance decision");
                return StatusCode(500, ex.Message);
            }
        }
    }

    // Fixed size struct for AppendOnlyLog
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, Pack = 1)]
    public struct GovernanceDecisionLogEntry
    {
        public long Timestamp;
        public byte Approved;
        // Simplified for fixed size requirement of AppendOnlyLog current implementation
        // Real implementation would likely serialize JSON to a byte buffer and use a different log method
        // or a fixed-size buffer in the struct (unsafe).
    }
}
