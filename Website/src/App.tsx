import {
  Route,
  RouterProvider,
  createBrowserRouter,
  createRoutesFromElements,
} from "react-router-dom";

import SupportLayout from "@/layout";
import SupportPage from "@/page";
import PageLoadingSpinner from "@/_components/PageLoadingSpinner";

function App() {
  const router = createBrowserRouter(
    createRoutesFromElements(
      <Route
        element={<SupportLayout />}
        hydrateFallbackElement={<PageLoadingSpinner text="Gersang Station..." />}
      >
        <Route element={<SupportPage />} path="/" />,
      </Route>
    ),
    {
      basename: "/" + import.meta.env.VITE_REPOSITORY_NAME,
    }
  );

  return <RouterProvider router={router} />;
}

export default App;
