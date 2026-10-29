import { Routes } from '@angular/router';
import { ActivityDetail } from './activities/activity-detail';
import { ActivityList } from './activities/activity-list';
import { RunningTrends } from './trends/running-trends';
import { DataManagement } from './data-management/data-management';

export const routes: Routes = [
  { path: 'trends', component: RunningTrends },
  { path: 'data', component: DataManagement },
  { path: 'activity/:id', component: ActivityDetail, data: { presentation: 'page' } },
  {
    path: '', component: ActivityList,
    children: [
      { path: 'activities/:id', component: ActivityDetail, data: { presentation: 'drawer' } },
    ],
  },
];
