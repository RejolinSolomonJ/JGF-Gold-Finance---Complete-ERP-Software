# JGF Jewel Gold Finance — 30-Day Parallel Run Protocol

---

## 1. Objectives & Scope

The purpose of the 30-Day Parallel Run is to operate the legacy jewel finance software and the new **JGF Finance Software** simultaneously before decommissioning the legacy system.

### Verification Standards:
1. **Zero-Rupee Discrepancy**: Total outstanding principal, interest collected, and daily cash balances must reconcile exactly between both systems.
2. **Number Series Alignment**: Pledge GL numbers, Receipt numbers, and Customer codes must remain synchronized.
3. **Weight & Bag Integrity**: Jewel gross weight, stone deduction, net weight, and packet counts must match 100%.

---

## 2. Daily Parallel Workflow (Days 1 to 30)

During each operational day:
1. **Entry Order**:
   - Customer onboarding, new pledges, part payments, and full closures are entered first into the existing legacy software, then immediately keyed into JGF (or vice versa).
2. **End-of-Day Dual Closing**:
   - Run the day-close procedure in the legacy system.
   - Run **Day Close (F9)** and **Integrity Audit** in JGF.
   - Print both daily summary reports and staple them together.

---

## 3. Weekly Reconciliation Checklist

Every Saturday at business close:

| Checkpoint | Target | Verified By |
| :--- | :--- | :--- |
| **Total Active Loans Count** | Must match exactly | Manager |
| **Total Principal Outstanding** | ₹0.00 difference | Owner |
| **Total Gold Weight in Locker** | Difference ≤ 0.005g | Owner |
| **Weekly Cash Collections** | ₹0.00 difference | Owner & Cashier |
| **Bank Re-Pledge Exposure** | Bank loan principal matches | Owner |
| **Automated Backup Check** | 7 valid dumps on USB | Administrator |

---

## 4. Final Cutover & Owner Sign-Off Criteria (Day 30)

At the conclusion of the 30-day period:
- [ ] 30 consecutive days of balanced daily cash reports without unexplained differences.
- [ ] At least one automated backup successfully restored to the drill test environment (`test-backup-restore.ps1`).
- [ ] Both PC1 and PC2 successfully handled concurrent transactions during peak rush hours without data lockouts or duplicate receipt numbers.
- [ ] All staff (Owner, Manager, Cashier) trained and proficient with JGF keyboard shortcuts and workflows.

**Owner Sign-off:**  
Signature: __________________________  
Date: _______________________________

