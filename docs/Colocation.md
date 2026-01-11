# Colocation Playbook

## Production Checklist (Equinix NY4 / CME Aurora)

- [ ] **Cross-Connect**: Verify light levels on fiber to exchange.
- [ ] **PTP Time**: Ensure `chrony` or `ptpd` is synced to hardware clock (under 100ns offset).
- [ ] **Compliance**: FIX drop-copy session active and logging to independent storage.
- [ ] **Servers**: Disable C-States, Turbo Boost, Hyper-threading (bios).

## Emergency Procedures

1. **Link Failure**: Logic must auto-cancel all open orders (Cancel-on-Disconnect enabled at Exchange).
2. **Algo Runaway**: "Big Red Button" script `scripts/halt.sh` drops network interface.
