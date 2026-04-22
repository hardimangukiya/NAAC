import { Component, OnInit } from '@@angular/core';

@@Component({
  selector: 'app-coordinator-dashboard',
  templateUrl: './dashboard.component.html',
  styleUrls: ['./dashboard.component.css']
})
export class DashboardComponent implements OnInit {
  academicYears: string[] = [];
  selectedYear: string = '';

  // 1. Dynamic Year Logic
  generateAcademicYears() {
    const currentYear = new Date().getFullYear();
    const currentMonth = new Date().getMonth(); // 0-indexed (May = 4, June = 5)

    let baseYear = (currentMonth >= 5) ? currentYear : currentYear - 1;
    this.selectedYear = `${baseYear}-${(baseYear + 1) % 100}`;

    for (let i = 0; i < 6; i++) {
      const year = baseYear - i;
      this.academicYears.push(`${year}-${(year + 1) % 100}`);
    }
  }

  // 2. Summary Cards Data
  stats = {
    totalFaculty: 45,
    totalCriteria: 13,
    totalRecords: 1280,
    missingData: 14,
    documentsUploaded: 92
  };

  // 3. Readiness Score
  readinessScore = 72; // (9/13 * 100)

  // 4. Criteria-wise Progress
  criteriaProgress = [
    { name: 'Criteria 5: Student Support', percentage: 85, status: 'Stable' },
    { name: 'Criteria 6: Governance', percentage: 42, status: 'Pending' },
    { name: 'Criteria 3: Research', percentage: 15, status: 'Critical' }
  ];

  // 5. Faculty vs Criteria
  facultyTable = [
    { name: 'Dr. Rahul Sharma', criteria: '5.1.1', status: 'Lead' },
    { name: 'Prof. Anjali Gupta', criteria: '6.3.3', status: 'Coordinator' },
    { name: 'Mr. Vivek Patel', criteria: '3.4.7', status: 'Faculty' }
  ];

  // 6. Alerts
  alerts = [
    { type: 'Missing Data', message: 'Table 5.2.1 has 0 records entered for current year.' },
    { type: 'Missing Document', message: 'AQAR Supporting document for 6.1.1 is missing.' }
  ];

  // 10. Notifications
  notifications = [
    { title: 'New Record', time: '2 mins ago', msg: 'Rahul added data in 5.1.1' },
    { title: 'Update', time: '1 hour ago', msg: 'Criteria 6 structure updated by Admin' }
  ];

  ngOnInit() {
    this.generateAcademicYears();
  }

  // 7. Management Functions
  addCriteria() { console.log('Add Criteria Flow'); }
  addTable() { console.log('Add Table Flow'); }
  editStructure() { console.log('Edit Structure Flow'); }

  // 8. Data View Filter
  onYearChange(year: string) {
    this.selectedYear = year;
    // Bind change to data reload logic
  }
}
