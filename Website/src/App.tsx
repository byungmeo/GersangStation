import {
  Route,
  RouterProvider,
  createBrowserRouter,
  createRoutesFromElements,
} from "react-router-dom";
import SupportLayout from "@/layout";

function App() {
  const router = createBrowserRouter(
    createRoutesFromElements(
      <Route element={<SupportLayout />} path="/">
        <Route path="faq" />,
        <Route path="notice" />,
      </Route>
    ),
    {
      basename: "/" + import.meta.env.VITE_REPOSITORY_NAME,
    }
  );

  return <RouterProvider router={router} fallbackElement={<p>Loading...</p>} />;
}

export default App;
