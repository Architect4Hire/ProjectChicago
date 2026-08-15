import { SidebarProvider, useSidebar } from "../context/SidebarContext";
import { Outlet } from "react-router";
import { type FC } from "react";
import AppHeader from "./AppHeader";
import Backdrop from "./Backdrop";
import AppSidebar from "./AppSidebar";

interface AppLayoutProps {
  sidebar?: FC;
}

const LayoutContent: React.FC<AppLayoutProps> = ({ sidebar: Sidebar = AppSidebar }) => {
  const { isExpanded, isHovered, isMobileOpen } = useSidebar();
  return (
    <div className="min-h-screen xl:flex">
      <div>
        <Sidebar />
        <Backdrop />
      </div>
      <div className={`flex-1 transition-all duration-300 ease-in-out ${isExpanded || isHovered ? "lg:ml-[290px]" : "lg:ml-[90px]"} ${isMobileOpen ? "ml-0" : ""}`}>
        <AppHeader />
        <div className="p-4 mx-auto max-w-(--breakpoint-2xl) md:p-6"><Outlet /></div>
      </div>
    </div>
  );
};

const AppLayout: FC<AppLayoutProps> = (props) => <SidebarProvider><LayoutContent {...props} /></SidebarProvider>;
export default AppLayout;
